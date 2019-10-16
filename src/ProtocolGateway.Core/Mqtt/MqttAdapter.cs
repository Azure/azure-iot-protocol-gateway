// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading;
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

    interface IConnectionIdentityProvider
    {
        string Id { get; }

        string ChannelId { get; }
    }

    public sealed class MqttAdapter : ChannelHandlerAdapter, IMessagingChannel, IConnectionIdentityProvider
    {
        public const string OperationScopeExceptionDataKey = "PG.MqttAdapter.Scope.Operation";
        public const string ChannelIdExceptionDataKey = "PG.MqttAdapter.Scope.ChannelId";
        public const string DeviceIdExceptionDataKey = "PG.MqttAdapter.Scope.DeviceId";

        const string InboundPublishProcessingScope = "-> PUBLISH";
        const string ReceiveProcessingScope = "Receive";
        const string ConnectProcessingScope = "Connect";
        const string ExceptionCaughtScope = "ExceptionCaught";

        static readonly Action<object> CheckConnectTimeoutCallback = CheckConnectionTimeout;
        static readonly Action<object> CheckKeepAliveCallback = CheckKeepAlive;
        static readonly Action<Task, object> ShutdownOnWriteFaultAction = (task, ctx) => ShutdownOnError((IChannelHandlerContext)ctx, "WriteAndFlushAsync", task.Exception);
        static readonly Action<Task, object> ShutdownOnPublishFaultAction = (task, ctx) => ShutdownOnError((IChannelHandlerContext)ctx, "<- PUBLISH", task.Exception);
        static readonly Action<Task, object> ShutdownOnPublishToServerFaultAction = CreateScopedFaultAction(InboundPublishProcessingScope);
        static readonly Action<Task, object> ShutdownOnPubAckFaultAction = CreateScopedFaultAction("-> PUBACK");
        static readonly Action<Task, object> ShutdownOnPubRecFaultAction = CreateScopedFaultAction("-> PUBREC");
        static readonly Action<Task, object> ShutdownOnPubCompFaultAction = CreateScopedFaultAction("-> PUBCOMP");

        IChannelHandlerContext capturedContext;
        readonly Settings settings;
        StateFlags stateFlags;
        readonly CancellationTokenSource lifetimeCancellation;
        DateTime lastClientActivityTime;
        ISessionState sessionState;
        Dictionary<IMessagingServiceClient, MessageAsyncProcessor<PublishPacket>> publishProcessors;
        RequestAckPairProcessor<AckPendingMessageState, PublishPacket> publishPubAckProcessor;
        RequestAckPairProcessor<AckPendingMessageState, PublishPacket> publishPubRecProcessor;
        RequestAckPairProcessor<CompletionPendingMessageState, PubRelPacket> pubRelPubCompProcessor;
        IMessagingBridge messagingBridge;
        readonly MessagingBridgeFactoryFunc messagingBridgeFactory;
        IDeviceIdentity identity;
        readonly IQos2StatePersistenceProvider qos2StateProvider;
        readonly QualityOfService maxSupportedQosToClient;
        TimeSpan keepAliveTimeout;
        Queue<Packet> subscriptionChangeQueue; // queue of SUBSCRIBE and UNSUBSCRIBE packets
        readonly ISessionStatePersistenceProvider sessionStateManager;
        readonly IDeviceIdentityProvider authProvider;
        Queue<Packet> connectPendingQueue;
        PublishPacket willPacket;
        SemaphoreSlim qos2Semaphore;
        event EventHandler capabilitiesChanged;

        public string ChannelId => this.capturedContext.Channel.Id.ToString();

        public string Id => this.identity?.ToString();

        SemaphoreSlim Qos2Semaphore => this.qos2Semaphore ?? (this.qos2Semaphore = new SemaphoreSlim(1, 1));

        public MqttAdapter(
            Settings settings,
            ISessionStatePersistenceProvider sessionStateManager,
            IDeviceIdentityProvider authProvider,
            IQos2StatePersistenceProvider qos2StateProvider,
            MessagingBridgeFactoryFunc messagingBridgeFactory)
        {
            Contract.Requires(settings != null);
            Contract.Requires(sessionStateManager != null);
            Contract.Requires(authProvider != null);
            Contract.Requires(messagingBridgeFactory != null);

            this.lifetimeCancellation = new CancellationTokenSource();

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

        bool ConnectedToService => this.messagingBridge != null;

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

            bool abortOnOutOfOrderAck = this.settings.AbortOnOutOfOrderPubAck;

            this.publishPubAckProcessor = new RequestAckPairProcessor<AckPendingMessageState, PublishPacket>(this.AcknowledgePublishAsync, abortOnOutOfOrderAck, this);
            this.publishPubAckProcessor.Closed.OnFault(ShutdownOnPubAckFaultAction, this);
            this.publishPubRecProcessor = new RequestAckPairProcessor<AckPendingMessageState, PublishPacket>(this.AcknowledgePublishReceiveAsync, abortOnOutOfOrderAck, this);
            this.publishPubRecProcessor.Closed.OnFault(ShutdownOnPubRecFaultAction, this);
            this.pubRelPubCompProcessor = new RequestAckPairProcessor<CompletionPendingMessageState, PubRelPacket>(this.AcknowledgePublishCompleteAsync, abortOnOutOfOrderAck, this);
            this.pubRelPubCompProcessor.Closed.OnFault(ShutdownOnPubCompFaultAction, this);

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
                CommonEventSource.Log.Warning($"Unexpected message (only `{typeof(Packet).FullName}` descendants are supported): {message}", this.ChannelId, this.Id);
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
                    ShutdownOnError(context, string.Empty, new ProtocolGatewayException(ErrorCode.ConnectExpected, $"First packet in the session must be CONNECT. Observed: {packet}, channel id: {this.ChannelId}, identity: {this.identity}"));
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
                            $"deviceId: {this.identity}",
                            this.ChannelId);
                    }
                    this.stateFlags |= StateFlags.ReadThrottled;
                }
            }
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            // For situations where the shutdown was initiated by us, we'd already be in a closed state and a better exception would be propagated.
            // In this case, the channel closure was beyond our control (device initiated).
            this.Shutdown(context, new ProtocolGatewayException(ErrorCode.ChannelClosed, "Channel closed."));

            base.ChannelInactive(context);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            ShutdownOnError(context, ExceptionCaughtScope, exception);
        }

        public override void UserEventTriggered(IChannelHandlerContext context, object evt)
        {
            if (evt is TlsHandshakeCompletionEvent handshakeEvent && !handshakeEvent.IsSuccessful)
            {
                CommonEventSource.Log.Warning("TLS handshake failed.", handshakeEvent.Exception, this.ChannelId, this.Id);
            }
        }

        #endregion

        void ProcessMessage(IChannelHandlerContext context, Packet packet)
        {
            if (this.IsInState(StateFlags.Closed))
            {
                CommonEventSource.Log.Warning($"Message was received after channel closure: {packet}", this.ChannelId, this.Id);
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
                    CommonEventSource.Log.Verbose("Disconnecting gracefully.", this.identity.ToString(), this.ChannelId);
                    this.Shutdown(context, null);
                    break;
                default:
                    ShutdownOnError(context, string.Empty, new ProtocolGatewayException(ErrorCode.UnknownPacketType, $"Packet of unsupported type was observed: {packet}, channel id: {this.ChannelId}, identity: {this.identity}"));
                    break;
            }
        }

        void ProcessPublish(IChannelHandlerContext context, PublishPacket packet)
        {
            if (!this.ConnectedToService)
            {
                return;
            }

            IMessagingServiceClient sendingClient = this.ResolveSendingClient(packet.TopicName);
            MessageAsyncProcessor<PublishPacket> publishProcessor;
            if (!this.publishProcessors.TryGetValue(sendingClient, out publishProcessor))
            {
                publishProcessor = new MessageAsyncProcessor<PublishPacket>((c, p) => this.PublishToServerAsync(c, sendingClient, p, null));
                publishProcessor.Closed.OnFault(ShutdownOnPublishToServerFaultAction, this);
                this.publishProcessors[sendingClient] = publishProcessor;
            }

            publishProcessor.Post(context, packet);
        }

        IMessagingServiceClient ResolveSendingClient(string topicName)
        {
            IMessagingServiceClient sendingClient;
            if (!this.messagingBridge.TryResolveClient(topicName, out sendingClient))
            {
                throw new ProtocolGatewayException(ErrorCode.UnResolvedSendingClient, $"Could not resolve a sending client based on topic name `{topicName}`.");
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

                    // save updated session state, make it current once successfully set
                    // we let the session manager decide how to handle transient state.
                    await this.sessionStateManager.SetAsync(this.identity, newState);

                    this.sessionState = newState;
                    this.capabilitiesChanged?.Invoke(this, EventArgs.Empty);

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
            if (!this.ConnectedToService)
            {
                packet.Release();
                return;
            }

            PreciseTimeSpan startedTimestamp = PreciseTimeSpan.FromStart;

            this.ResumeReadingIfNecessary(context);

            IMessage message = null;
            try
            {
                message = sendingClient.CreateMessage(packet.TopicName, packet.Payload);
                Util.CompleteMessageFromPacket(message, packet, this.settings);

                if (messageType != null)
                {
                    message.Properties[this.settings.ServicePropertyPrefix + MessagePropertyNames.MessageType] = messageType;
                }

                await sendingClient.SendAsync(message);

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
                            ShutdownOnError(context, InboundPublishProcessingScope, new ProtocolGatewayException(ErrorCode.ExactlyOnceQosNotSupported, "QoS 2 is not supported."));
                            break;
                        default:
                            throw new ProtocolGatewayException(ErrorCode.UnknownQosType, "Unexpected QoS level: " + packet.QualityOfService.ToString());
                    }
                }
                message = null;
            }
            finally
            {
                message?.Dispose();
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
                        CommonEventSource.Log.Verbose("Resuming reading from channel as queue freed up.", $"deviceId: {this.identity}", this.ChannelId);
                    }
                    context.Read();
                }
            }
        }

        #endregion

        #region PUBLISH Server -> Client handling

        void IMessagingChannel.Handle(IMessage message, IMessagingSource callback)
        {
            if (this.capturedContext.Executor.InEventLoop)
            {
                this.HandleInternal(message, callback);
            }
            else
            {
                this.capturedContext.Executor.Execute((s, m) => this.HandleInternal((IMessage)m, (IMessagingSource)s), callback, message);
            }
        }

        void HandleInternal(IMessage message, IMessagingSource sender)
        {
            IChannelHandlerContext context = this.capturedContext;
            try
            {
                Contract.Assert(message != null);

                PerformanceCounters.MessagesReceivedPerSecond.Increment();
                this.PublishToClientAsync(context, message, sender).OnFault(ShutdownOnPublishFaultAction, context);
                message = null;
            }
            catch (MessagingException ex)
            {
                this.ShutdownOnReceiveError(ex);
            }
            catch (Exception ex)
            {
                ShutdownOnError(context, ReceiveProcessingScope, ex);
            }
            finally
            {
                message?.Payload.SafeRelease();
            }
        }

        void IMessagingChannel.Close(Exception cause)
        {
            if (this.capturedContext.Executor.InEventLoop)
            {
                this.ShutdownOnReceiveError(cause);
            }
            else
            {
                this.capturedContext.Executor.Execute(() => this.ShutdownOnReceiveError(cause));
            }
        }

        event EventHandler IMessagingChannel.CapabilitiesChanged
        {
            add { this.capabilitiesChanged += value; }
            remove { this.capabilitiesChanged -= value; }
        }

        async Task PublishToClientAsync(IChannelHandlerContext context, IMessage message, IMessagingSource callback)
        {
            PublishPacket packet = null;
            try
            {
                using (message)
                {
                    message.Properties[TemplateParameters.DeviceIdTemplateParam] = this.DeviceId;

                    QualityOfService qos;
                    QualityOfService maxRequestedQos;
                    if (this.TryMatchSubscription(message.Address, message.CreatedTimeUtc, out maxRequestedQos))
                    {
                        qos = Util.DeriveQos(message, this.settings, this.ChannelId, this.Id);
                        if (maxRequestedQos < qos)
                        {
                            qos = maxRequestedQos;
                        }
                    }
                    else
                    {
                        // no matching subscription found - complete the message without publishing
                        await RejectMessageAsync(message.Id, callback);
                        return;
                    }

                    packet = Util.ComposePublishPacket(context, message, qos, context.Channel.Allocator);
                    switch (qos)
                    {
                        case QualityOfService.AtMostOnce:
                            await this.PublishToClientQos0Async(context, message, callback, packet);
                            break;
                        case QualityOfService.AtLeastOnce:
                            await this.PublishToClientQos1Async(context, message, callback, packet);
                            break;
                        case QualityOfService.ExactlyOnce:
                            if (this.maxSupportedQosToClient >= QualityOfService.ExactlyOnce)
                            {
                                await this.PublishToClientQos2Async(context, message, callback, packet);
                            }
                            else
                            {
                                throw new ProtocolGatewayException(ErrorCode.QoSLevelNotSupported, "Requested QoS level is not supported.");
                            }
                            break;
                        default:
                            throw new ProtocolGatewayException(ErrorCode.QoSLevelNotSupported, "Requested QoS level is not supported.");
                    }
                }
                this.lastClientActivityTime = DateTime.UtcNow; // note last client activity - used in handling disconnects on keep-alive timeout
            }
            catch (Exception ex)
            {
                ReferenceCountUtil.SafeRelease(packet);
                ShutdownOnError(context, "<- PUBLISH", ex);
            }
        }

        static async Task RejectMessageAsync(string messageId, IMessagingSource callback)
        {
            await callback.RejectAsync(messageId); // awaiting guarantees that we won't complete consecutive message before this is completed.
            PerformanceCounters.MessagesRejectedPerSecond.Increment();
        }

        Task PublishToClientQos0Async(IChannelHandlerContext context, IMessage message, IMessagingSource callback, PublishPacket packet)
        {
            if (message.DeliveryCount == 0)
            {
                return Task.WhenAll(
                    callback.CompleteAsync(message.Id),
                    Util.WriteMessageAsync(context, packet));
            }
            else
            {
                return callback.CompleteAsync(message.Id);
            }
        }

        Task PublishToClientQos1Async(IChannelHandlerContext context, IMessage message, IMessagingSource callback, PublishPacket packet)
        {
            return this.publishPubAckProcessor.SendRequestAsync(context, packet,
                new AckPendingMessageState(message, callback, packet));
        }

        async Task PublishToClientQos2Async(IChannelHandlerContext context, IMessage message, IMessagingSource callback, PublishPacket packet)
        {
            await Qos2Semaphore.WaitAsync(this.lifetimeCancellation.Token); // this ensures proper ordering of messages

            Task deliveryTask;
            try
            {
                int packetId = packet.PacketId;
                IQos2MessageDeliveryState messageInfo = await this.qos2StateProvider.GetMessageAsync(this.identity, packetId);

                if (messageInfo != null && message.SequenceNumber != messageInfo.SequenceNumber)
                {
                    await this.qos2StateProvider.DeleteMessageAsync(this.identity, packetId, messageInfo);
                    messageInfo = null;
                }

                if (messageInfo == null)
                {
                    deliveryTask = this.publishPubRecProcessor.SendRequestAsync(context, packet,
                        new AckPendingMessageState(message, callback, packet));
                }
                else
                {
                    deliveryTask = this.PublishReleaseToClientAsync(context, packetId, new MessageFeedbackChannel(message.Id, callback), messageInfo, PreciseTimeSpan.FromStart);
                }
            }
            finally
            {
                try
                {
                    this.Qos2Semaphore.Release();
                }
                catch (ObjectDisposedException)
                { }
            }

            await deliveryTask;
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
            }
            catch (Exception ex)
            {
                ShutdownOnError(context, "-> PUBCOMP", ex);
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
                    && subscription.CreationTime <= messageTime
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

        async void ShutdownOnReceiveError(Exception cause)
        {
            this.publishPubAckProcessor.Close();
            foreach (var publishProcessor in this.publishProcessors)
            {
                publishProcessor.Value.Close();
            }
            this.publishPubRecProcessor.Close();
            this.pubRelPubCompProcessor.Close();

            IMessagingBridge bridge = this.messagingBridge;
            if (bridge != null)
            {
                this.messagingBridge = null;
                try
                {
                    await bridge.DisposeAsync(cause);
                }
                catch (Exception ex)
                {
                    CommonEventSource.Log.Info("Failed to close IoT Hub Client cleanly: " + ex, this.ChannelId, this.Id);
                }
            }
            ShutdownOnError(this.capturedContext, ReceiveProcessingScope, cause);
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
                    ShutdownOnError(context, ConnectProcessingScope, new ProtocolGatewayException(ErrorCode.DuplicateConnectReceived, "CONNECT has been received in current session already. Only one CONNECT is expected per session."));
                    return;
                }

                this.stateFlags = StateFlags.ProcessingConnect;
                this.identity = await this.authProvider.GetAsync(packet.ClientId,
                    packet.Username, packet.Password, context.Channel.RemoteAddress);

                if (!this.identity.IsAuthenticated)
                {
                    CommonEventSource.Log.Info("ClientNotAuthenticated", this.ChannelId, $"Client ID: {packet.ClientId}; Username: {packet.Username}");
                    connAckSent = true;
                    await Util.WriteMessageAsync(context, new ConnAckPacket
                    {
                        ReturnCode = ConnectReturnCode.RefusedNotAuthorized
                    });
                    PerformanceCounters.ConnectionFailedAuthPerSecond.Increment();
                    ShutdownOnError(context, ConnectProcessingScope, new ProtocolGatewayException(ErrorCode.AuthenticationFailed, "Authentication failed."));
                    return;
                }

                CommonEventSource.Log.Info("ClientAuthenticated", this.ChannelId, this.Id);

                this.messagingBridge = await this.messagingBridgeFactory(this.identity, this.lifetimeCancellation.Token);

                bool sessionPresent = await this.EstablishSessionStateAsync(packet.CleanSession);

                this.keepAliveTimeout = this.DeriveKeepAliveTimeout(context, packet);

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
                            CommonEventSource.Log.Verbose("Error sending 'Server Unavailable' CONNACK:" + ex, this.ChannelId, this.Id);
                        }
                    }
                }

                ShutdownOnError(context, ConnectProcessingScope, exception);
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

        TimeSpan DeriveKeepAliveTimeout(IChannelHandlerContext context, ConnectPacket packet)
        {
            TimeSpan timeout = TimeSpan.FromSeconds(packet.KeepAliveInSeconds * 1.5);
            TimeSpan? maxTimeout = this.settings.MaxKeepAliveTimeout;
            if (maxTimeout.HasValue && (timeout > maxTimeout.Value || timeout == TimeSpan.Zero))
            {
                if (CommonEventSource.Log.IsVerboseEnabled)
                {
                    CommonEventSource.Log.Verbose($"Requested Keep Alive timeout is longer than the max allowed. Limiting to max value of {maxTimeout.Value}.", this.ChannelId, this.Id);
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
            CommonEventSource.Log.Info("Connection established.", this.ChannelId, this.Id);

            if (this.keepAliveTimeout > TimeSpan.Zero)
            {
                CheckKeepAlive(context);
            }

            this.messagingBridge.BindMessagingChannel(this);
            this.stateFlags = StateFlags.Connected;

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
                ShutdownOnError(context, string.Empty, new ProtocolGatewayException(ErrorCode.ConnectionTimedOut, "Connection timed out on waiting for CONNECT packet from client."));
            }
        }

        static void CheckKeepAlive(object ctx)
        {
            var context = (IChannelHandlerContext)ctx;
            var self = (MqttAdapter)context.Handler;
            TimeSpan elapsedSinceLastActive = DateTime.UtcNow - self.lastClientActivityTime;
            if (elapsedSinceLastActive > self.keepAliveTimeout)
            {
                ShutdownOnError(context, string.Empty, new ProtocolGatewayException(ErrorCode.KeepAliveTimedOut, "Keep Alive timed out."));
                return;
            }

            context.Channel.EventLoop.ScheduleAsync(CheckKeepAliveCallback, context, self.keepAliveTimeout - elapsedSinceLastActive);
        }

        /// <summary>
        ///     Initiates closure of both channel and hub connection.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="scope">Scope where error has occurred.</param>
        /// <param name="error">Exception describing the error leading to closure.</param>
        static void ShutdownOnError(IChannelHandlerContext context, string scope, Exception error)
        {
            var self = (MqttAdapter)context.Handler;
            if (!self.IsInState(StateFlags.Closed))
            {
                if (error != null && !string.IsNullOrEmpty(scope))
                {
                    error.Data[OperationScopeExceptionDataKey] = scope;
                    error.Data[ChannelIdExceptionDataKey] = context.Channel.Id.ToString();
                    error.Data[DeviceIdExceptionDataKey] = self.Id;
                }

                PerformanceCounters.ConnectionFailedOperationalPerSecond.Increment();
                self.Shutdown(context, error);
            }
        }

        /// <summary>
        ///     Closes channel
        /// </summary>
        async void Shutdown(IChannelHandlerContext context, Exception cause)
        {
            if (this.IsInState(StateFlags.Closed))
            {
                return;
            }

            this.lifetimeCancellation.Cancel();
            this.qos2Semaphore?.Dispose();

            try
            {
                this.stateFlags |= StateFlags.Closed; // "or" not to interfere with ongoing logic which has to honor Closed state when it's right time to do (case by case)

                // only decrement connection current counter if the state had connected state in this session 
                if (this.IsInState(StateFlags.Connected))
                {
                    PerformanceCounters.ConnectionsCurrent.Decrement();
                }

                Queue<Packet> connectQueue = this.connectPendingQueue;
                if (connectQueue != null)
                {
                    while (connectQueue.Count > 0)
                    {
                        Packet packet = connectQueue.Dequeue();
                        ReferenceCountUtil.Release(packet);
                    }
                }

                PublishPacket will = (cause != null) && this.IsInState(StateFlags.Connected) ? this.willPacket : null;

                await this.CloseServiceConnection(context, cause, will);
                await context.CloseAsync();
            }
            catch (Exception ex)
            {
                CommonEventSource.Log.Warning("Error occurred while shutting down the channel.", ex, this.ChannelId, this.Id);
            }
        }

        async Task CloseServiceConnection(IChannelHandlerContext context, Exception cause, PublishPacket will)
        {
            if (!this.ConnectedToService)
            {
                // closure happened before IoT Hub connection was established or it was initiated due to disconnect
                return;
            }

            try
            {
                this.publishPubAckProcessor.Close();
                this.publishPubRecProcessor.Close();
                this.pubRelPubCompProcessor.Close();

                await Task.WhenAll(
                    this.CompletePublishAsync(context, will),
                    this.publishPubAckProcessor.Closed,
                    this.publishPubRecProcessor.Closed,
                    this.pubRelPubCompProcessor.Closed);
            }
            catch (Exception ex)
            {
                CommonEventSource.Log.Info("Failed to complete the processors: " + ex.ToString(), this.ChannelId, this.Id);
            }

            try
            {
                IMessagingBridge bridge = this.messagingBridge;
                if (this.messagingBridge != null)
                {
                    this.messagingBridge = null;
                    await bridge.DisposeAsync(cause);
                }
            }
            catch (Exception ex)
            {
                CommonEventSource.Log.Info("Failed to close IoT Hub Client cleanly: " + ex.ToString(), this.ChannelId, this.Id);
            }
        }

        async Task CompletePublishAsync(IChannelHandlerContext context, PublishPacket will)
        {
            var completionTasks = new List<Task>();
            foreach (var publishProcessorRecord in this.publishProcessors)
            {
                publishProcessorRecord.Value.Close();
                completionTasks.Add(publishProcessorRecord.Value.Closed);
            }
            await Task.WhenAll(completionTasks);

            IMessagingServiceClient sendingClient = null;
            if (will != null)
            {
                sendingClient = this.ResolveSendingClient(will.TopicName);
                try
                {
                    await this.PublishToServerAsync(context, sendingClient, will, MessageTypes.Will);
                }
                catch (Exception ex)
                {
                    CommonEventSource.Log.Warning("Failed sending Will Message.", ex, this.ChannelId, this.Id);
                }
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

        static Action<Task, object> CreateScopedFaultAction(string scope)
        {
            return (task, state) =>
            {
                var self = (MqttAdapter)state;
                // ReSharper disable once PossibleNullReferenceException // called in case of fault only, so task.Exception is never null
                var ex = task.Exception.InnerException as ChannelMessageProcessingException;
                if (ex != null)
                {
                    ShutdownOnError(ex.Context, scope, task.Exception);
                }
                else
                {
                    CommonEventSource.Log.Error($"{scope}: exception occurred", task.Exception, self.ChannelId, self.Id);
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