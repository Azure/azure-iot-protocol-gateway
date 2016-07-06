// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Channels;
    using Microsoft.Azure.Devices.ProtocolGateway.Extensions;
    using Microsoft.Azure.Devices.ProtocolGateway.Identity;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.Messaging;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;

    public sealed class MqttAdapter : ChannelHandlerAdapter, IMessagingChannel<MessageWithFeedback>
    {
        static readonly Action<object> CheckConnectTimeoutCallback = CheckConnectionTimeout;
        static readonly Action<object> CheckKeepAliveCallback = CheckKeepAlive;
        static readonly Action<Task, object> ShutdownOnWriteFaultAction = (task, ctx) => ShutdownOnError((IChannelHandlerContext)ctx, "WriteAndFlushAsync", task.Exception);
        static readonly Action<Task, object> ShutdownOnPublishFaultAction = (task, ctx) => ShutdownOnError((IChannelHandlerContext)ctx, "<- PUBLISH", task.Exception);
        static readonly Action<Task> ShutdownOnPublishToServerFaultAction = CreateScopedFaultAction("-> PUBLISH");
        static readonly Action<Task> ShutdownOnPubAckFaultAction = CreateScopedFaultAction("-> PUBACK");
        static readonly Action<Task> ShutdownOnPubRecFaultAction = CreateScopedFaultAction("-> PUBREC");
        static readonly Action<Task> ShutdownOnPubCompFaultAction = CreateScopedFaultAction("-> PUBCOMP");

        IChannelHandlerContext capturedContext;
        readonly Settings settings;
        StateFlags stateFlags;
        DateTime lastClientActivityTime;
        ISessionState sessionState;
        Dictionary<IMessagingServiceClient, MessageAsyncProcessor<PublishPacket>> publishProcessors;
        RequestAckPairProcessor<AckPendingMessageState, PublishPacket> publishPubAckProcessor;
        RequestAckPairProcessor<AckPendingMessageState, PublishPacket> publishPubRecProcessor;
        RequestAckPairProcessor<CompletionPendingMessageState, PubRelPacket> pubRelPubCompProcessor;
        IMqttMessagingBridge messagingBridge;
        readonly MqttBridgeFactoryFunc messagingBridgeFactory;
        IDeviceIdentity identity;
        readonly IQos2StatePersistenceProvider qos2StateProvider;
        readonly QualityOfService maxSupportedQosToClient;
        TimeSpan keepAliveTimeout;
        Queue<Packet> subscriptionChangeQueue; // queue of SUBSCRIBE and UNSUBSCRIBE packets
        readonly ISessionStatePersistenceProvider sessionStateManager;
        readonly IDeviceIdentityProvider authProvider;
        Queue<Packet> connectPendingQueue;
        PublishPacket willPacket;

        public MqttAdapter(
            Settings settings,
            ISessionStatePersistenceProvider sessionStateManager,
            IDeviceIdentityProvider authProvider,
            IQos2StatePersistenceProvider qos2StateProvider,
            MqttBridgeFactoryFunc messagingBridgeFactory)
        {
            Contract.Requires(settings != null);
            Contract.Requires(sessionStateManager != null);
            Contract.Requires(authProvider != null);
            Contract.Requires(messagingBridgeFactory != null);

            if (qos2StateProvider != null)
            {
                this.maxSupportedQosToClient = QualityOfService.ExactlyOnce;
                this.qos2StateProvider = qos2StateProvider;
            }
            else
            {
                this.maxSupportedQosToClient = QualityOfService.AtLeastOnce;
            }

            this.settings = settings;
            this.sessionStateManager = sessionStateManager;
            this.authProvider = authProvider;
            this.messagingBridgeFactory = messagingBridgeFactory;
        }

        bool ConnectedToHub => this.messagingBridge != null;

        string DeviceId => this.identity.Id;

        int InboundBacklogSize =>
            this.publishPubAckProcessor.BacklogSize
            + this.publishPubRecProcessor.BacklogSize
            + this.pubRelPubCompProcessor.BacklogSize;

        #region IChannelHandler overrides

        public override void ChannelActive(IChannelHandlerContext context)
        {
            this.capturedContext = context;

            this.publishProcessors = new Dictionary<IMessagingServiceClient, MessageAsyncProcessor<PublishPacket>>(1);

            TimeSpan? ackTimeout = this.settings.DeviceReceiveAckCanTimeout ? this.settings.DeviceReceiveAckTimeout : (TimeSpan?)null;
            this.publishPubAckProcessor = new RequestAckPairProcessor<AckPendingMessageState, PublishPacket>(this.AcknowledgePublishAsync, this.RetransmitNextPublish, ackTimeout);
            this.publishPubAckProcessor.Completion.OnFault(ShutdownOnPubAckFaultAction);
            this.publishPubRecProcessor = new RequestAckPairProcessor<AckPendingMessageState, PublishPacket>(this.AcknowledgePublishReceiveAsync, this.RetransmitNextPublish, ackTimeout);
            this.publishPubRecProcessor.Completion.OnFault(ShutdownOnPubRecFaultAction);
            this.pubRelPubCompProcessor = new RequestAckPairProcessor<CompletionPendingMessageState, PubRelPacket>(this.AcknowledgePublishCompleteAsync, this.RetransmitNextPublishRelease, ackTimeout);
            this.pubRelPubCompProcessor.Completion.OnFault(ShutdownOnPubCompFaultAction);

            this.stateFlags = StateFlags.WaitingForConnect;
            TimeSpan? timeout = this.settings.ConnectArrivalTimeout;
            if (timeout.HasValue)
            {
                context.Executor.ScheduleAsync(CheckConnectTimeoutCallback, context, timeout.Value);
            }
            base.ChannelActive(context);

            context.Read();
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            var packet = message as Packet;
            if (packet == null)
            {
                CommonEventSource.Log.Warning($"Unexpected message (only `{typeof(Packet).FullName}` descendants are supported): {message}");
                return;
            }

            this.lastClientActivityTime = DateTime.UtcNow; // notice last client activity - used in handling disconnects on keep-alive timeout

            if (this.IsInState(StateFlags.Connected) || packet.PacketType == PacketType.CONNECT)
            {
                this.ProcessMessage(context, packet);
            }
            else
            {
                if (this.IsInState(StateFlags.ProcessingConnect))
                {
                    Queue<Packet> queue = this.connectPendingQueue ?? (this.connectPendingQueue = new Queue<Packet>(4));
                    queue.Enqueue(packet);
                }
                else
                {
                    // we did not start processing CONNECT yet which means we haven't received it yet but the packet of different type has arrived.
                    ShutdownOnError(context, $"First packet in the session must be CONNECT. Observed: {packet}");
                }
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            base.ChannelReadComplete(context);
            if (!this.IsInState(StateFlags.ReadThrottled))
            {
                if (this.IsReadAllowed())
                {
                    context.Read();
                }
                else
                {
                    if (CommonEventSource.Log.IsVerboseEnabled)
                    {
                        CommonEventSource.Log.Verbose(
                            "Not reading per full inbound message queue",
                            $"deviceId: {this.identity}, channel: {context.Channel}");
                    }
                    this.stateFlags &= StateFlags.ReadThrottled;
                }
            }
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            this.Shutdown(context, false);

            base.ChannelInactive(context);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            ShutdownOnError(context, "Exception encountered: " + exception);
        }

        public override void UserEventTriggered(IChannelHandlerContext context, object @event)
        {
            var handshakeCompletionEvent = @event as TlsHandshakeCompletionEvent;
            if (handshakeCompletionEvent != null && !handshakeCompletionEvent.IsSuccessful)
            {
                CommonEventSource.Log.Warning("TLS handshake failed.", handshakeCompletionEvent.Exception);
            }
        }

        #endregion

        void ProcessMessage(IChannelHandlerContext context, Packet packet)
        {
            if (this.IsInState(StateFlags.Closed))
            {
                CommonEventSource.Log.Warning($"Message was received after channel closure: {packet}");
                return;
            }

            PerformanceCounters.PacketsReceivedPerSecond.Increment();

            switch (packet.PacketType)
            {
                case PacketType.CONNECT:
                    this.Connect(context, (ConnectPacket)packet);
                    break;
                case PacketType.PUBLISH:
                    PerformanceCounters.PublishPacketsReceivedPerSecond.Increment();
                    this.ProcessPublish(context, (PublishPacket)packet);
                    break;
                case PacketType.PUBACK:
                    this.publishPubAckProcessor.Post(context, (PubAckPacket)packet);
                    break;
                case PacketType.PUBREC:
                    this.publishPubRecProcessor.Post(context, (PubRecPacket)packet);
                    break;
                case PacketType.PUBCOMP:
                    this.pubRelPubCompProcessor.Post(context, (PubCompPacket)packet);
                    break;
                case PacketType.SUBSCRIBE:
                case PacketType.UNSUBSCRIBE:
                    this.HandleSubscriptionChange(context, packet);
                    break;
                case PacketType.PINGREQ:
                    // no further action is needed - keep-alive "timer" was reset by now
                    Util.WriteMessageAsync(context, PingRespPacket.Instance)
                        .OnFault(ShutdownOnWriteFaultAction, context);
                    break;
                case PacketType.DISCONNECT:
                    CommonEventSource.Log.Verbose("Disconnecting gracefully.", this.identity.ToString());
                    this.Shutdown(context, true);
                    break;
                default:
                    ShutdownOnError(context, $"Packet of unsupported type was observed: {packet}");
                    break;
            }
        }

        void ProcessPublish(IChannelHandlerContext context, PublishPacket packet)
        {
            if (!this.ConnectedToHub)
            {
                return;
            }

            IMessagingServiceClient sendingClient = this.ResolveSendingClient(packet.TopicName);
            MessageAsyncProcessor<PublishPacket> publishProcessor;
            if (!this.publishProcessors.TryGetValue(sendingClient, out publishProcessor))
            {
                publishProcessor = new MessageAsyncProcessor<PublishPacket>((c, p) => this.PublishToServerAsync(c, sendingClient, p, null));
                publishProcessor.Completion.OnFault(ShutdownOnPublishToServerFaultAction);
                this.publishProcessors[sendingClient] = publishProcessor;
            }

            publishProcessor.Post(context, packet);
        }

        IMessagingServiceClient ResolveSendingClient(string topicName)
        {
            IMessagingServiceClient sendingClient;
            if (!this.messagingBridge.TryResolveClient(topicName, out sendingClient))
            {
                throw new InvalidOperationException($"Could not resolve a sending client based on topic name `{topicName}`.");
            }
            return sendingClient;
        }

        #region SUBSCRIBE / UNSUBSCRIBE handling

        void HandleSubscriptionChange(IChannelHandlerContext context, Packet packet)
        {
            Queue<Packet> changeQueue = this.subscriptionChangeQueue;
            if (changeQueue == null)
            {
                this.subscriptionChangeQueue = changeQueue = new Queue<Packet>(4);
            }
            changeQueue.Enqueue(packet);

            if (!this.IsInState(StateFlags.ChangingSubscriptions))
            {
                this.stateFlags |= StateFlags.ChangingSubscriptions;
                this.ProcessPendingSubscriptionChanges(context);
            }
        }

        async void ProcessPendingSubscriptionChanges(IChannelHandlerContext context)
        {
            try
            {
                do
                {
                    ISessionState newState = this.sessionState.Copy();
                    Queue<Packet> queue = this.subscriptionChangeQueue;
                    Contract.Assert(queue != null);

                    var acks = new List<Packet>(queue.Count);
                    foreach (Packet packet in queue) // todo: if can queue be null here, don't force creation
                    {
                        switch (packet.PacketType)
                        {
                            case PacketType.SUBSCRIBE:
                                acks.Add(Util.AddSubscriptions(newState, (SubscribePacket)packet, this.maxSupportedQosToClient));
                                break;
                            case PacketType.UNSUBSCRIBE:
                                acks.Add(Util.RemoveSubscriptions(newState, (UnsubscribePacket)packet));
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                    queue.Clear();

                    if (!this.sessionState.IsTransient)
                    {
                        // save updated session state, make it current once successfully set
                        await this.sessionStateManager.SetAsync(this.identity, newState);
                    }

                    this.sessionState = newState;

                    // release ACKs

                    var tasks = new List<Task>(acks.Count);
                    foreach (Packet ack in acks)
                    {
                        tasks.Add(context.WriteAsync(ack));
                    }
                    context.Flush();
                    await Task.WhenAll(tasks);
                    PerformanceCounters.PacketsSentPerSecond.IncrementBy(acks.Count);
                }
                while (this.subscriptionChangeQueue.Count > 0);

                this.subscriptionChangeQueue = null;

                this.ResetState(StateFlags.ChangingSubscriptions);
            }
            catch (Exception ex)
            {
                ShutdownOnError(context, "-> UN/SUBSCRIBE", ex);
            }
        }

        #endregion

        #region PUBLISH Client -> Server handling

        async Task PublishToServerAsync(IChannelHandlerContext context, IMessagingServiceClient sendingClient, PublishPacket packet, string messageType)
        {
            if (!this.ConnectedToHub)
            {
                return;
            }

            PreciseTimeSpan startedTimestamp = PreciseTimeSpan.FromStart;

            this.ResumeReadingIfNecessary(context);

            using (IMessage message = sendingClient.CreateMessage(packet.TopicName, packet.Payload))
            {
                Util.CompleteMessageFromPacket(message, packet, this.settings);

                if (messageType != null)
                {
                    message.Properties[this.settings.ServicePropertyPrefix + MessagePropertyNames.MessageType] = messageType;
                }

                await sendingClient.SendAsync(message);
            }

            PerformanceCounters.MessagesSentPerSecond.Increment();

            if (!this.IsInState(StateFlags.Closed))
            {
                switch (packet.QualityOfService)
                {
                    case QualityOfService.AtMostOnce:
                        // no response necessary
                        PerformanceCounters.InboundMessageProcessingTime.Register(startedTimestamp);
                        break;
                    case QualityOfService.AtLeastOnce:
                        Util.WriteMessageAsync(context, PubAckPacket.InResponseTo(packet))
                            .OnFault(ShutdownOnWriteFaultAction, context);
                        PerformanceCounters.InboundMessageProcessingTime.Register(startedTimestamp); // todo: assumes PUBACK is written out sync
                        break;
                    case QualityOfService.ExactlyOnce:
                        ShutdownOnError(context, "QoS 2 is not supported.");
                        break;
                    default:
                        throw new InvalidOperationException("Unexpected QoS level: " + packet.QualityOfService.ToString());
                }
            }
        }

        void ResumeReadingIfNecessary(IChannelHandlerContext context)
        {
            if (this.IsInState(StateFlags.ReadThrottled))
            {
                if (this.IsReadAllowed()) // we picked up a packet from full queue - now we have more room so order another read
                {
                    this.ResetState(StateFlags.ReadThrottled);
                    if (CommonEventSource.Log.IsVerboseEnabled)
                    {
                        CommonEventSource.Log.Verbose("Resuming reading from channel as queue freed up.", $"deviceId: {this.identity}, channel: {context.Channel}");
                    }
                }
                context.Read();
            }
        }

        #endregion

        #region PUBLISH Server -> Client handling

        public async void Handle(MessageWithFeedback messageWithFeedback)
        {
            IChannelHandlerContext context = this.capturedContext;
            try
            {
                IMessage message = messageWithFeedback.Message;
                Contract.Assert(message != null);

                PerformanceCounters.MessagesReceivedPerSecond.Increment();

                int processorsInRetransmission = 0;
                bool sentThroughRetransmission = false;

                if (this.publishPubAckProcessor.Retransmitting)
                {
                    processorsInRetransmission++;
                    AckPendingMessageState pendingPubAck = this.publishPubAckProcessor.FirstRequestPendingAck;
                    if (pendingPubAck.SequenceNumber == message.SequenceNumber)
                    {
                        this.RetransmitPublishMessage(context, messageWithFeedback, pendingPubAck);
                        sentThroughRetransmission = true;
                    }
                }

                if (this.publishPubRecProcessor.Retransmitting)
                {
                    processorsInRetransmission++;
                    if (!sentThroughRetransmission)
                    {
                        AckPendingMessageState pendingPubRec = this.publishPubRecProcessor.FirstRequestPendingAck;
                        if (pendingPubRec.SequenceNumber == message.SequenceNumber)
                        {
                            this.RetransmitPublishMessage(context, messageWithFeedback, pendingPubRec);
                            sentThroughRetransmission = true;
                        }
                    }
                }

                if (processorsInRetransmission == 0)
                {
                    this.PublishToClientAsync(context, messageWithFeedback).OnFault(ShutdownOnPublishFaultAction, context);
                }
                else
                {
                    if (!sentThroughRetransmission)
                    {
                        // message id is different - "publish" this message (it actually will be enqueued for future retransmission immediately)
                        await this.PublishToClientAsync(context, messageWithFeedback);
                        // todo: consider back pressure in a form of explicit retransmission state communication with MSC
                    }
                }
            }
            catch (MessagingException ex)
            {
                this.ShutdownOnReceiveError(ex.ToString());
            }
            catch (Exception ex)
            {
                ShutdownOnError(context, "Receive", ex.ToString());
            }
        }

        public void Close(Exception exception) => this.ShutdownOnReceiveError(exception?.ToString());

        async Task PublishToClientAsync(IChannelHandlerContext context, MessageWithFeedback messageWithFeedback)
        {
            PublishPacket packet = null;
            try
            {
                using (IMessage message = messageWithFeedback.Message)
                {
                    message.Properties[TemplateParameters.DeviceIdTemplateParam] = this.DeviceId;

                    QualityOfService qos;
                    QualityOfService maxRequestedQos;
                    if (this.TryMatchSubscription(message.Address, message.CreatedTimeUtc, out maxRequestedQos))
                    {
                        qos = Util.DeriveQos(message, this.settings);
                        if (maxRequestedQos < qos)
                        {
                            qos = maxRequestedQos;
                        }
                    }
                    else
                    {
                        // no matching subscription found - complete the message without publishing
                        await RejectMessageAsync(messageWithFeedback);
                        return;
                    }

                    packet = Util.ComposePublishPacket(context, message, qos, context.Channel.Allocator);
                    switch (qos)
                    {
                        case QualityOfService.AtMostOnce:
                            await this.PublishToClientQos0Async(context, messageWithFeedback, packet);
                            break;
                        case QualityOfService.AtLeastOnce:
                            await this.PublishToClientQos1Async(context, messageWithFeedback, packet);
                            break;
                        case QualityOfService.ExactlyOnce:
                            if (this.maxSupportedQosToClient >= QualityOfService.ExactlyOnce)
                            {
                                await this.PublishToClientQos2Async(context, messageWithFeedback, packet);
                            }
                            else
                            {
                                throw new InvalidOperationException("Requested QoS level is not supported.");
                            }
                            break;
                        default:
                            throw new InvalidOperationException("Requested QoS level is not supported.");
                    }
                }
            }
            catch (Exception ex)
            {
                ReferenceCountUtil.SafeRelease(packet);
                ShutdownOnError(context, "<- PUBLISH", ex);
            }
        }

        static async Task RejectMessageAsync(MessageWithFeedback messageWithFeedback)
        {
            await messageWithFeedback.FeedbackChannel.RejectAsync(); // awaiting guarantees that we won't complete consecutive message before this is completed.
            PerformanceCounters.MessagesRejectedPerSecond.Increment();
        }

        Task PublishToClientQos0Async(IChannelHandlerContext context, MessageWithFeedback messageWithFeedback, PublishPacket packet)
        {
            if (messageWithFeedback.Message.DeliveryCount == 0)
            {
                return Task.WhenAll(
                    messageWithFeedback.FeedbackChannel.CompleteAsync(),
                    Util.WriteMessageAsync(context, packet));
            }
            else
            {
                return messageWithFeedback.FeedbackChannel.CompleteAsync();
            }
        }

        Task PublishToClientQos1Async(IChannelHandlerContext context, MessageWithFeedback messageWithFeedback, PublishPacket packet)
        {
            return this.publishPubAckProcessor.SendRequestAsync(context, packet,
                new AckPendingMessageState(messageWithFeedback, packet));
        }

        async Task PublishToClientQos2Async(IChannelHandlerContext context, MessageWithFeedback messageWithFeedback, PublishPacket packet)
        {
            int packetId = packet.PacketId;
            IQos2MessageDeliveryState messageInfo = await this.qos2StateProvider.GetMessageAsync(this.identity, packetId);

            if (messageInfo != null && messageWithFeedback.Message.SequenceNumber != messageInfo.SequenceNumber)
            {
                await this.qos2StateProvider.DeleteMessageAsync(this.identity, packetId, messageInfo);
                messageInfo = null;
            }

            if (messageInfo == null)
            {
                await this.publishPubRecProcessor.SendRequestAsync(context, packet,
                    new AckPendingMessageState(messageWithFeedback, packet));
            }
            else
            {
                await this.PublishReleaseToClientAsync(context, packetId, messageWithFeedback.FeedbackChannel, messageInfo, PreciseTimeSpan.FromStart);
            }
        }

        Task PublishReleaseToClientAsync(IChannelHandlerContext context, int packetId, MessageFeedbackChannel feedbackChannel,
            IQos2MessageDeliveryState messageState, PreciseTimeSpan startTimestamp)
        {
            var pubRelPacket = new PubRelPacket();
            pubRelPacket.PacketId = packetId;
            return this.pubRelPubCompProcessor.SendRequestAsync(context, pubRelPacket,
                new CompletionPendingMessageState(packetId, messageState, startTimestamp, feedbackChannel));
        }

        async Task AcknowledgePublishAsync(IChannelHandlerContext context, AckPendingMessageState message)
        {
            this.ResumeReadingIfNecessary(context);

            // todo: is try-catch needed here?
            try
            {
                await message.FeedbackChannel.CompleteAsync();

                PerformanceCounters.OutboundMessageProcessingTime.Register(message.StartTimestamp);

                this.publishPubAckProcessor.ResumeRetransmission(context);
            }
            catch (Exception ex)
            {
                ShutdownOnError(context, "-> PUBACK", ex);
            }
        }

        async Task AcknowledgePublishReceiveAsync(IChannelHandlerContext context, AckPendingMessageState message)
        {
            this.ResumeReadingIfNecessary(context);

            // todo: is try-catch needed here?
            try
            {
                IQos2MessageDeliveryState messageInfo = this.qos2StateProvider.Create(message.SequenceNumber);
                await this.qos2StateProvider.SetMessageAsync(this.identity, message.PacketId, messageInfo);

                await this.PublishReleaseToClientAsync(context, message.PacketId, message.FeedbackChannel, messageInfo, message.StartTimestamp);

                this.publishPubRecProcessor.ResumeRetransmission(context);
            }
            catch (Exception ex)
            {
                ShutdownOnError(context, "-> PUBREC", ex);
            }
        }

        async Task AcknowledgePublishCompleteAsync(IChannelHandlerContext context, CompletionPendingMessageState message)
        {
            this.ResumeReadingIfNecessary(context);

            try
            {
                await message.FeedbackChannel.CompleteAsync();

                await this.qos2StateProvider.DeleteMessageAsync(this.identity, message.PacketId, message.DeliveryState);

                PerformanceCounters.OutboundMessageProcessingTime.Register(message.StartTimestamp);

                this.pubRelPubCompProcessor.ResumeRetransmission(context);
            }
            catch (Exception ex)
            {
                ShutdownOnError(context, "-> PUBCOMP", ex);
            }
        }

        async void RetransmitNextPublish(IChannelHandlerContext context, AckPendingMessageState messageInfo)
        {
            try
            {
                await messageInfo.FeedbackChannel.AbandonAsync();
            }
            catch (MessagingException ex)
            {
                this.ShutdownOnReceiveError(ex.ToString());
            }
            catch (Exception ex)
            {
                ShutdownOnError(context, ex.ToString());
            }
        }

        async void RetransmitPublishMessage(IChannelHandlerContext context, MessageWithFeedback messageWithFeedback, AckPendingMessageState messageInfo)
        {
            PublishPacket packet = null;
            try
            {
                using (IMessage message = messageWithFeedback.Message)
                {
                    message.Properties[TemplateParameters.DeviceIdTemplateParam] = this.DeviceId;
                    packet = Util.ComposePublishPacket(context, message, messageInfo.QualityOfService, context.Channel.Allocator);
                    messageInfo.ResetMessage(message, messageWithFeedback.FeedbackChannel);
                    await this.publishPubAckProcessor.RetransmitAsync(context, packet, messageInfo);
                }
            }
            catch (Exception ex)
            {
                ReferenceCountUtil.SafeRelease(packet);
                ShutdownOnError(context, "<- PUBLISH (retransmission)", ex);
            }
        }

        async void RetransmitNextPublishRelease(IChannelHandlerContext context, CompletionPendingMessageState messageInfo)
        {
            try
            {
                var packet = new PubRelPacket
                {
                    PacketId = messageInfo.PacketId
                };
                await this.pubRelPubCompProcessor.RetransmitAsync(context, packet, messageInfo);
            }
            catch (Exception ex)
            {
                ShutdownOnError(context, "<- PUBREL (retransmission)", ex);
            }
        }

        bool TryMatchSubscription(string topicName, DateTime messageTime, out QualityOfService qos)
        {
            bool found = false;
            qos = QualityOfService.AtMostOnce;
            IReadOnlyList<ISubscription> subscriptions = this.sessionState.Subscriptions;
            for (int i = 0; i < subscriptions.Count; i++)
            {
                ISubscription subscription = subscriptions[i];
                if ((!found || subscription.QualityOfService > qos)
                    && subscription.CreationTime < messageTime
                    && Util.CheckTopicFilterMatch(topicName, subscription.TopicFilter))
                {
                    found = true;
                    qos = subscription.QualityOfService;
                    if (qos >= this.maxSupportedQosToClient)
                    {
                        qos = this.maxSupportedQosToClient;
                        break;
                    }
                }
            }
            return found;
        }

        async void ShutdownOnReceiveError(string exception)
        {
            this.publishPubAckProcessor.Abort();
            foreach (var publishProcessor in this.publishProcessors)
            {
                publishProcessor.Value.Abort();
            }
            this.publishPubRecProcessor.Abort();
            this.pubRelPubCompProcessor.Abort();

            IMqttMessagingBridge bridge = this.messagingBridge;
            if (bridge != null)
            {
                this.messagingBridge = null;
                try
                {
                    await bridge.DisposeAsync();
                }
                catch (Exception ex)
                {
                    CommonEventSource.Log.Info("Failed to close IoT Hub Client cleanly.", ex.ToString());
                }
            }
            ShutdownOnError(this.capturedContext, "Receive", exception);
        }

        #endregion

        #region CONNECT handling and lifecycle management

        /// <summary>
        ///     Performs complete initialization of <see cref="MqttAdapter" /> based on received CONNECT packet.
        /// </summary>
        /// <param name="context"><see cref="IChannelHandlerContext" /> instance.</param>
        /// <param name="packet">CONNECT packet.</param>
        async void Connect(IChannelHandlerContext context, ConnectPacket packet)
        {
            bool connAckSent = false;

            Exception exception = null;
            try
            {
                if (!this.IsInState(StateFlags.WaitingForConnect))
                {
                    ShutdownOnError(context, "CONNECT has been received in current session already. Only one CONNECT is expected per session.");
                    return;
                }

                this.stateFlags = StateFlags.ProcessingConnect;
                this.identity = await this.authProvider.GetAsync(packet.ClientId,
                    packet.Username, packet.Password, context.Channel.RemoteAddress);
                if (!this.identity.IsAuthenticated)
                {
                    connAckSent = true;
                    await Util.WriteMessageAsync(context, new ConnAckPacket
                    {
                        ReturnCode = ConnectReturnCode.RefusedNotAuthorized
                    });
                    PerformanceCounters.ConnectionFailedAuthPerSecond.Increment();
                    ShutdownOnError(context, "Authentication failed.");
                    return;
                }

                this.messagingBridge = await this.messagingBridgeFactory(this.identity);

                bool sessionPresent = await this.EstablishSessionStateAsync(packet.CleanSession);

                this.keepAliveTimeout = this.DeriveKeepAliveTimeout(packet);

                if (packet.HasWill)
                {
                    var will = new PublishPacket(packet.WillQualityOfService, false, packet.WillRetain);
                    will.TopicName = packet.WillTopicName;
                    will.Payload = packet.WillMessage;
                    this.willPacket = will;
                }

                connAckSent = true;
                await Util.WriteMessageAsync(context, new ConnAckPacket
                {
                    SessionPresent = sessionPresent,
                    ReturnCode = ConnectReturnCode.Accepted
                });

                this.CompleteConnect(context);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            if (exception != null)
            {
                if (!connAckSent)
                {
                    try
                    {
                        await Util.WriteMessageAsync(context, new ConnAckPacket
                        {
                            ReturnCode = ConnectReturnCode.RefusedServerUnavailable
                        });
                    }
                    catch (Exception ex)
                    {
                        if (CommonEventSource.Log.IsVerboseEnabled)
                        {
                            CommonEventSource.Log.Verbose("Error sending 'Server Unavailable' CONNACK.", ex.ToString());
                        }
                    }
                }

                ShutdownOnError(context, "CONNECT", exception);
            }
        }

        /// <summary>
        ///     Loads and updates (as necessary) session state.
        /// </summary>
        /// <param name="cleanSession">Determines whether session has to be deleted if it already exists.</param>
        /// <returns></returns>
        async Task<bool> EstablishSessionStateAsync(bool cleanSession)
        {
            ISessionState existingSessionState = await this.sessionStateManager.GetAsync(this.identity);
            if (cleanSession)
            {
                if (existingSessionState != null)
                {
                    await this.sessionStateManager.DeleteAsync(this.identity, existingSessionState);
                    // todo: loop in case of concurrent access? how will we resolve conflict with concurrent connections?
                }

                this.sessionState = this.sessionStateManager.Create(true);
                return false;
            }
            else
            {
                if (existingSessionState == null)
                {
                    this.sessionState = this.sessionStateManager.Create(false);
                    return false;
                }
                else
                {
                    this.sessionState = existingSessionState;
                    return true;
                }
            }
        }

        TimeSpan DeriveKeepAliveTimeout(ConnectPacket packet)
        {
            TimeSpan timeout = TimeSpan.FromSeconds(packet.KeepAliveInSeconds * 1.5);
            TimeSpan? maxTimeout = this.settings.MaxKeepAliveTimeout;
            if (maxTimeout.HasValue && (timeout > maxTimeout.Value || timeout == TimeSpan.Zero))
            {
                if (CommonEventSource.Log.IsVerboseEnabled)
                {
                    CommonEventSource.Log.Verbose($"Requested Keep Alive timeout is longer than the max allowed. Limiting to max value of {maxTimeout.Value}.", null);
                }
                return maxTimeout.Value;
            }

            return timeout;
        }

        /// <summary>
        ///     Finalizes initialization based on CONNECT packet: dispatches keep-alive timer and releases messages buffered before
        ///     the CONNECT processing was finalized.
        /// </summary>
        /// <param name="context"><see cref="IChannelHandlerContext" /> instance.</param>
        void CompleteConnect(IChannelHandlerContext context)
        {
            CommonEventSource.Log.Verbose("Connection established.", this.identity.ToString());

            if (this.keepAliveTimeout > TimeSpan.Zero)
            {
                CheckKeepAlive(context);
            }

            this.stateFlags = StateFlags.Connected;

            this.messagingBridge.BindMessagingChannel(this);

            PerformanceCounters.ConnectionsEstablishedTotal.Increment();
            PerformanceCounters.ConnectionsCurrent.Increment();
            PerformanceCounters.ConnectionsEstablishedPerSecond.Increment();

            if (this.connectPendingQueue != null)
            {
                while (this.connectPendingQueue.Count > 0)
                {
                    Packet packet = this.connectPendingQueue.Dequeue();
                    this.ProcessMessage(context, packet);
                }
                this.connectPendingQueue = null; // release unnecessary queue
            }
        }

        static void CheckConnectionTimeout(object state)
        {
            var context = (IChannelHandlerContext)state;
            var handler = (MqttAdapter)context.Handler;
            if (handler.IsInState(StateFlags.WaitingForConnect))
            {
                ShutdownOnError(context, "Connection timed out on waiting for CONNECT packet from client.");
            }
        }

        static void CheckKeepAlive(object ctx)
        {
            var context = (IChannelHandlerContext)ctx;
            var self = (MqttAdapter)context.Handler;
            TimeSpan elapsedSinceLastActive = DateTime.UtcNow - self.lastClientActivityTime;
            if (elapsedSinceLastActive > self.keepAliveTimeout)
            {
                ShutdownOnError(context, "Keep Alive timed out.");
                return;
            }

            context.Channel.EventLoop.ScheduleAsync(CheckKeepAliveCallback, context, self.keepAliveTimeout - elapsedSinceLastActive);
        }

        static void ShutdownOnError(IChannelHandlerContext context, string scope, Exception exception) => ShutdownOnError(context, scope, exception.ToString());

        static void ShutdownOnError(IChannelHandlerContext context, string scope, string exception) => ShutdownOnError(context, $"Exception occured ({scope}): {exception}");

        /// <summary>
        ///     Logs error and initiates closure of both channel and hub connection.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="reason">Explanation for channel closure.</param>
        static void ShutdownOnError(IChannelHandlerContext context, string reason)
        {
            Contract.Requires(!string.IsNullOrEmpty(reason));

            var self = (MqttAdapter)context.Handler;
            if (!self.IsInState(StateFlags.Closed))
            {
                PerformanceCounters.ConnectionFailedOperationalPerSecond.Increment();
                CommonEventSource.Log.Warning($"Closing connection: {self.identity}", reason);
                self.Shutdown(context, false);
            }
        }

        /// <summary>
        ///     Closes channel
        /// </summary>
        /// <param name="context"></param>
        /// <param name="graceful"></param>
        async void Shutdown(IChannelHandlerContext context, bool graceful)
        {
            if (this.IsInState(StateFlags.Closed))
            {
                return;
            }

            try
            {
                this.stateFlags |= StateFlags.Closed; // "or" not to interfere with ongoing logic which has to honor Closed state when it's right time to do (case by case)

                PerformanceCounters.ConnectionsCurrent.Decrement();

                Queue<Packet> connectQueue = this.connectPendingQueue;
                if (connectQueue != null)
                {
                    while (connectQueue.Count > 0)
                    {
                        Packet packet = connectQueue.Dequeue();
                        ReferenceCountUtil.Release(packet);
                    }
                }

                PublishPacket will = !graceful && this.IsInState(StateFlags.Connected) ? this.willPacket : null;

                this.CloseIotHubConnection(context, will);
                await context.CloseAsync();
            }
            catch (Exception ex)
            {
                CommonEventSource.Log.Warning("Error occurred while shutting down the channel.", ex);
            }
        }

        async void CloseIotHubConnection(IChannelHandlerContext context, PublishPacket will)
        {
            if (!this.ConnectedToHub)
            {
                // closure happened before IoT Hub connection was established or it was initiated due to disconnect
                return;
            }

            try
            {
                foreach (var publishProcessor in this.publishProcessors)
                {
                    publishProcessor.Value.Complete();
                }
                this.publishPubAckProcessor.Complete();
                this.publishPubRecProcessor.Complete();
                this.pubRelPubCompProcessor.Complete();
                await Task.WhenAll(
                    this.CompletePublishAsync(context, will),
                    this.publishPubAckProcessor.Completion,
                    this.publishPubRecProcessor.Completion,
                    this.pubRelPubCompProcessor.Completion);

                IMqttMessagingBridge hub = this.messagingBridge;
                this.messagingBridge = null;
                await hub.DisposeAsync();
            }
            catch (Exception ex)
            {
                CommonEventSource.Log.Info("Failed to close IoT Hub Client cleanly.", ex.ToString());
            }
        }

        async Task CompletePublishAsync(IChannelHandlerContext context, PublishPacket will)
        {
            if (will == null)
            {
                return;
            }

            var sendingClient = this.ResolveSendingClient(will.TopicName);

            MessageAsyncProcessor<PublishPacket> publishProcessor;
            if (this.publishProcessors.TryGetValue(sendingClient, out publishProcessor))
            {
                await publishProcessor.Completion;
            }

            try
            {
                await this.PublishToServerAsync(context, sendingClient, will, MessageTypes.Will);
            }
            catch (Exception ex)
            {
                CommonEventSource.Log.Warning("Failed sending Will Message.", ex);
            }
        }

        #endregion

        #region helper methods

        bool IsReadAllowed()
        {
            if (this.InboundBacklogSize >= this.settings.MaxPendingInboundAcknowledgements)
            {
                return false;
            }

            foreach (var pair in this.publishProcessors)
            {
                if (pair.Value.BacklogSize >= pair.Key.MaxPendingMessages)
                {
                    return false;
                }
            }

            return true;
        }

        static Action<Task> CreateScopedFaultAction(string scope)
        {
            return task =>
            {
                // ReSharper disable once PossibleNullReferenceException // called in case of fault only, so task.Exception is never null
                var ex = task.Exception.InnerException as ChannelMessageProcessingException;
                if (ex != null)
                {
                    ShutdownOnError(ex.Context, scope, task.Exception);
                }
                else
                {
                    CommonEventSource.Log.Error($"{scope}: exception occurred", task.Exception);
                }
            };
        }

        bool IsInState(StateFlags stateFlagsToCheck) => (this.stateFlags & stateFlagsToCheck) == stateFlagsToCheck;

        bool ResetState(StateFlags stateFlagsToReset)
        {
            StateFlags flags = this.stateFlags;
            this.stateFlags = flags & ~stateFlagsToReset;
            return (flags & stateFlagsToReset) != 0;
        }

        #endregion

        [Flags]
        enum StateFlags
        {
            WaitingForConnect = 1,
            ProcessingConnect = 1 << 1,
            Connected = 1 << 2,
            ChangingSubscriptions = 1 << 3,
            Closed = 1 << 4,
            ReadThrottled = 1 << 5
        }
    }
}