// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.IO;
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
    using Microsoft.Azure.Devices.ProtocolGateway.Routing;

    public sealed class MqttIotHubAdapter : ChannelHandlerAdapter
    {
        static readonly Action<object> CheckConnectTimeoutCallback = CheckConnectionTimeout;
        static readonly Action<object> CheckKeepAliveCallback = CheckKeepAlive;
        static readonly Action<Task, object> ShutdownOnWriteFaultAction = (task, ctx) => ShutdownOnError((IChannelHandlerContext)ctx, "WriteAndFlushAsync", task.Exception);
        static readonly Action<Task, object> ShutdownOnPublishFaultAction = (task, ctx) => ShutdownOnError((IChannelHandlerContext)ctx, "<- PUBLISH", task.Exception);
        static readonly Action<Task> ShutdownOnPublishToServerFaultAction = CreateScopedFaultAction("-> PUBLISH");
        static readonly Action<Task> ShutdownOnPubAckFaultAction = CreateScopedFaultAction("-> PUBACK");
        static readonly Action<Task> ShutdownOnPubRecFaultAction = CreateScopedFaultAction("-> PUBREC");
        static readonly Action<Task> ShutdownOnPubCompFaultAction = CreateScopedFaultAction("-> PUBCOMP");

        readonly Settings settings;
        StateFlags stateFlags;
        IMessagingServiceClient messagingServiceClient;
        DateTime lastClientActivityTime;
        ISessionState sessionState;
        readonly MessageAsyncProcessor<PublishPacket> publishProcessor;
        readonly RequestAckPairProcessor<AckPendingMessageState, PublishPacket> publishPubAckProcessor;
        readonly RequestAckPairProcessor<AckPendingMessageState, PublishPacket> publishPubRecProcessor;
        readonly RequestAckPairProcessor<CompletionPendingMessageState, PubRelPacket> pubRelPubCompProcessor;
        readonly IMessageRouter messageRouter;
        readonly IMessagingFactory messagingFactory;
        IDeviceIdentity identity;
        readonly IQos2StatePersistenceProvider qos2StateProvider;
        readonly QualityOfService maxSupportedQosToClient;
        TimeSpan keepAliveTimeout;
        Queue<Packet> subscriptionChangeQueue; // queue of SUBSCRIBE and UNSUBSCRIBE packets
        readonly ISessionStatePersistenceProvider sessionStateManager;
        readonly IDeviceIdentityProvider authProvider;
        Queue<Packet> connectPendingQueue;
        PublishPacket willPacket;

        public MqttIotHubAdapter(
            Settings settings,
            ISessionStatePersistenceProvider sessionStateManager,
            IDeviceIdentityProvider authProvider,
            IQos2StatePersistenceProvider qos2StateProvider,
            IMessagingFactory messagingFactory,
            IMessageRouter messageRouter)
        {
            Contract.Requires(settings != null);
            Contract.Requires(sessionStateManager != null);
            Contract.Requires(authProvider != null);
            Contract.Requires(messageRouter != null);

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
            this.messagingFactory = messagingFactory;
            this.messageRouter = messageRouter;

            this.publishProcessor = new MessageAsyncProcessor<PublishPacket>(this.PublishToServerAsync);
            this.publishProcessor.Completion.OnFault(ShutdownOnPublishToServerFaultAction);

            TimeSpan? ackTimeout = this.settings.DeviceReceiveAckCanTimeout ? this.settings.DeviceReceiveAckTimeout : (TimeSpan?)null;
            this.publishPubAckProcessor = new RequestAckPairProcessor<AckPendingMessageState, PublishPacket>(this.AcknowledgePublishAsync, this.RetransmitNextPublish, ackTimeout);
            this.publishPubAckProcessor.Completion.OnFault(ShutdownOnPubAckFaultAction);
            this.publishPubRecProcessor = new RequestAckPairProcessor<AckPendingMessageState, PublishPacket>(this.AcknowledgePublishReceiveAsync, this.RetransmitNextPublish, ackTimeout);
            this.publishPubRecProcessor.Completion.OnFault(ShutdownOnPubRecFaultAction);
            this.pubRelPubCompProcessor = new RequestAckPairProcessor<CompletionPendingMessageState, PubRelPacket>(this.AcknowledgePublishCompleteAsync, this.RetransmitNextPublishRelease, ackTimeout);
            this.pubRelPubCompProcessor.Completion.OnFault(ShutdownOnPubCompFaultAction);
        }

        Queue<Packet> SubscriptionChangeQueue => this.subscriptionChangeQueue ?? (this.subscriptionChangeQueue = new Queue<Packet>(4));

        Queue<Packet> ConnectPendingQueue => this.connectPendingQueue ?? (this.connectPendingQueue = new Queue<Packet>(4));

        bool ConnectedToHub => this.messagingServiceClient != null;

        string DeviceId => this.identity.Id;

        int InboundBacklogSize =>
            this.publishProcessor.BacklogSize
                + this.publishPubAckProcessor.BacklogSize
                + this.publishPubRecProcessor.BacklogSize
                + this.pubRelPubCompProcessor.BacklogSize;

        int MessagePendingAckCount =>
            this.publishPubAckProcessor.RequestPendingAckCount
                + this.publishPubRecProcessor.RequestPendingAckCount
                + this.pubRelPubCompProcessor.RequestPendingAckCount;

        #region IChannelHandler overrides

        public override void ChannelActive(IChannelHandlerContext context)
        {
            this.stateFlags = StateFlags.WaitingForConnect;
            TimeSpan? timeout = this.settings.ConnectArrivalTimeout;
            if (timeout.HasValue)
            {
                context.Channel.EventLoop.ScheduleAsync(CheckConnectTimeoutCallback, context, timeout.Value);
            }
            base.ChannelActive(context);

            context.Read();
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            var packet = message as Packet;
            if (packet == null)
            {
                MqttIotHubAdapterEventSource.Log.Warning($"Unexpected message (only `{typeof(Packet).FullName}` descendants are supported): {message}");
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
                    this.ConnectPendingQueue.Enqueue(packet);
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
            int inboundBacklogSize = this.InboundBacklogSize;
            if (inboundBacklogSize < this.settings.MaxPendingInboundMessages)
            {
                context.Read();
            }
            else
            {
                if (MqttIotHubAdapterEventSource.Log.IsVerboseEnabled)
                {
                    MqttIotHubAdapterEventSource.Log.Verbose(
                        "Not reading per full inbound message queue",
                        $"deviceId: {this.identity}, messages queued: {inboundBacklogSize}, channel: {context.Channel}");
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
                MqttIotHubAdapterEventSource.Log.Warning("TLS handshake failed.", handshakeCompletionEvent.Exception);
            }
        }

        #endregion

        void ProcessMessage(IChannelHandlerContext context, Packet packet)
        {
            if (this.IsInState(StateFlags.Closed))
            {
                MqttIotHubAdapterEventSource.Log.Warning($"Message was received after channel closure: {packet}");
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
                    this.publishProcessor.Post(context, (PublishPacket)packet);
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
                    MqttIotHubAdapterEventSource.Log.Verbose("Disconnecting gracefully.", this.identity.ToString());
                    this.Shutdown(context, true);
                    break;
                default:
                    ShutdownOnError(context, $"Packet of unsupported type was observed: {packet}");
                    break;
            }
        }

        #region SUBSCRIBE / UNSUBSCRIBE handling

        void HandleSubscriptionChange(IChannelHandlerContext context, Packet packet)
        {
            this.SubscriptionChangeQueue.Enqueue(packet);

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
                    Queue<Packet> queue = this.SubscriptionChangeQueue;
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

                this.ResetState(StateFlags.ChangingSubscriptions);
            }
            catch (Exception ex)
            {
                ShutdownOnError(context, "-> UN/SUBSCRIBE", ex);
            }
        }

        #endregion

        #region PUBLISH Client -> Server handling

        Task PublishToServerAsync(IChannelHandlerContext context, PublishPacket packet)
        {
            return this.PublishToServerAsync(context, packet, null);
        }

        async Task PublishToServerAsync(IChannelHandlerContext context, PublishPacket packet, string messageType)
        {
            if (!this.ConnectedToHub)
            {
                return;
            }

            PreciseTimeSpan startedTimestamp = PreciseTimeSpan.FromStart;

            this.ResumeReadingIfNecessary(context);

            using (Stream bodyStream = packet.Payload.IsReadable() ? new ReadOnlyByteBufferStream(packet.Payload, true) : null)
            using (IMessage message = this.messagingFactory.CreateMessage(bodyStream))
            {
                this.ApplyRoutingConfiguration(message, packet);

                Util.CompleteMessageFromPacket(message, packet, this.settings);

                if (messageType != null)
                {
                    message.Properties[this.settings.ServicePropertyPrefix + MessagePropertyNames.MessageType] = messageType;
                }

                await this.messagingServiceClient.SendAsync(message);

                PerformanceCounters.MessagesSentPerSecond.Increment();
            }

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
                        throw new InvalidOperationException("Unexpected QoS level: " + packet.QualityOfService);
                }
            }
        }

        void ResumeReadingIfNecessary(IChannelHandlerContext context)
        {
            if (this.InboundBacklogSize == this.settings.MaxPendingInboundMessages - 1) // we picked up a packet from full queue - now we have more room so order another read
            {
                if (MqttIotHubAdapterEventSource.Log.IsVerboseEnabled)
                {
                    MqttIotHubAdapterEventSource.Log.Verbose("Resuming reading from channel as queue freed up.", $"deviceId: {this.identity}, channel: {context.Channel}");
                }
                context.Read();
            }
        }

        void ApplyRoutingConfiguration(IMessage message, PublishPacket packet)
        {
            RouteDestinationType routeType;
            if (this.messageRouter.TryRouteIncomingMessage(packet.TopicName, message, out routeType))
            {
                // successfully matched topic against configured routes -> validate topic name
                string messageDeviceId;
                if (message.Properties.TryGetValue(TemplateParameters.DeviceIdTemplateParam, out messageDeviceId))
                {
                    if (!this.DeviceId.Equals(messageDeviceId, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Device ID provided in topic name ({messageDeviceId}) does not match ID of the device publishing message ({this.DeviceId})");
                    }
                    message.Properties.Remove(TemplateParameters.DeviceIdTemplateParam);
                }
            }
            else
            {
                if (!this.settings.PassThroughUnmatchedMessages)
                {
                    throw new InvalidOperationException($"Topic name `{packet.TopicName}` could not be matched against any of the configured routes.");
                }

                if (MqttIotHubAdapterEventSource.Log.IsWarningEnabled)
                {
                    MqttIotHubAdapterEventSource.Log.Warning("Topic name could not be matched against any of the configured routes. Falling back to default telemetry settings.", packet.ToString());
                }
                routeType = RouteDestinationType.Telemetry;
                message.Properties[this.settings.ServicePropertyPrefix + MessagePropertyNames.Unmatched] = bool.TrueString;
                message.Properties[this.settings.ServicePropertyPrefix + MessagePropertyNames.Subject] = packet.TopicName;
            }

            // once we have different routes, this will change to tackle different aspects of route types
            switch (routeType)
            {
                case RouteDestinationType.Telemetry:
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unexpected route type: {routeType}");
            }
        }

        #endregion

        #region PUBLISH Server -> Client handling

        async void Receive(IChannelHandlerContext context)
        {
            try
            {
                IMessage message = await this.messagingServiceClient.ReceiveAsync();
                if (message == null)
                {
                    // link to IoT Hub has been closed
                    this.ShutdownOnReceiveError(context, null);
                    return;
                }

                PerformanceCounters.MessagesReceivedPerSecond.Increment();

                bool receiving = this.IsInState(StateFlags.Receiving);
                int processorsInRetransmission = 0;
                bool messageSent = false;

                if (this.publishPubAckProcessor.Retransmitting)
                {
                    processorsInRetransmission++;
                    AckPendingMessageState pendingPubAck = this.publishPubAckProcessor.FirstRequestPendingAck;
                    if (pendingPubAck.SequenceNumber == message.SequenceNumber)
                    {
                        this.RetransmitPublishMessage(context, message, pendingPubAck);
                        messageSent = true;
                    }
                }

                if (this.publishPubRecProcessor.Retransmitting)
                {
                    processorsInRetransmission++;
                    if (!messageSent)
                    {
                        AckPendingMessageState pendingPubRec = this.publishPubRecProcessor.FirstRequestPendingAck;
                        if (pendingPubRec.SequenceNumber == message.SequenceNumber)
                        {
                            this.RetransmitPublishMessage(context, message, pendingPubRec);
                            messageSent = true;
                        }
                    }
                }

                if (processorsInRetransmission == 0)
                {
                    this.PublishToClientAsync(context, message).OnFault(ShutdownOnPublishFaultAction, context);
                    if (!this.IsInState(StateFlags.Closed)
                        && (this.MessagePendingAckCount < this.settings.MaxPendingOutboundMessages))
                    {
                        this.Receive(context); // todo: review for potential stack depth issues
                    }
                    else
                    {
                        this.ResetState(StateFlags.Receiving);
                    }
                }
                else
                {
                    if (receiving)
                    {
                        if (!messageSent)
                        {
                            // message id is different - "publish" this message (it actually will be enqueued for future retransmission immediately)
                            await this.PublishToClientAsync(context, message);
                        }
                        this.ResetState(StateFlags.Receiving);
                        for (int i = processorsInRetransmission - (messageSent ? 1 : 0); i > 0; i--)
                        {
                            // fire receive for processors that went into retransmission but held off receiving messages due to ongoing receive call
                            this.Receive(context); // todo: review for potential stack depth issues
                        }
                    }
                    else if (!messageSent)
                    {
                        throw new InvalidOperationException("Received a message that does not match");
                    }
                }
            }
            catch (MessagingException ex)
            {
                this.ShutdownOnReceiveError(context, ex.ToString());
            }
            catch (Exception ex)
            {
                ShutdownOnError(context, "Receive", ex.ToString());
            }
        }

        async Task PublishToClientAsync(IChannelHandlerContext context, IMessage message)
        {
            try
            {
                using (message)
                {
                    if (this.settings.MaxOutboundRetransmissionEnforced && message.DeliveryCount > this.settings.MaxOutboundRetransmissionCount)
                    {
                        await this.RejectMessageAsync(message);
                        return;
                    }

                    string topicName;
                    message.Properties[TemplateParameters.DeviceIdTemplateParam] = this.DeviceId;
                    if (!this.messageRouter.TryRouteOutgoingMessage(RouteSourceType.Notification, message, out topicName))
                    {
                        // source is not configured
                        await this.RejectMessageAsync(message);
                        return;
                    }

                    QualityOfService qos;
                    QualityOfService maxRequestedQos;
                    if (this.TryMatchSubscription(topicName, message.CreatedTimeUtc, out maxRequestedQos))
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
                        await this.RejectMessageAsync(message);
                        return;
                    }

                    PublishPacket packet = await Util.ComposePublishPacketAsync(context, message, qos, topicName, context.Channel.Allocator);
                    switch (qos)
                    {
                        case QualityOfService.AtMostOnce:
                            await this.PublishToClientQos0Async(context, message, packet);
                            break;
                        case QualityOfService.AtLeastOnce:
                            await this.PublishToClientQos1Async(context, message, packet);
                            break;
                        case QualityOfService.ExactlyOnce:
                            if (this.maxSupportedQosToClient >= QualityOfService.ExactlyOnce)
                            {
                                await this.PublishToClientQos2Async(context, message, packet);
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
                // todo: log more details
                ShutdownOnError(context, "<- PUBLISH", ex);
            }
        }

        async Task RejectMessageAsync(IMessage message)
        {
            await this.messagingServiceClient.RejectAsync(message.LockToken); // awaiting guarantees that we won't complete consecutive message before this is completed.
            PerformanceCounters.MessagesRejectedPerSecond.Increment();
        }

        Task PublishToClientQos0Async(IChannelHandlerContext context, IMessage message, PublishPacket packet)
        {
            if (message.DeliveryCount == 0)
            {
                return Task.WhenAll(
                    this.messagingServiceClient.CompleteAsync(message.LockToken),
                    Util.WriteMessageAsync(context, packet));
            }
            else
            {
                return this.messagingServiceClient.CompleteAsync(message.LockToken);
            }
        }

        Task PublishToClientQos1Async(IChannelHandlerContext context, IMessage message, PublishPacket packet)
        {
            return this.publishPubAckProcessor.SendRequestAsync(context, packet, new AckPendingMessageState(message, packet));
        }

        async Task PublishToClientQos2Async(IChannelHandlerContext context, IMessage message, PublishPacket packet)
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
                await this.publishPubRecProcessor.SendRequestAsync(context, packet, new AckPendingMessageState(message, packet));
            }
            else
            {
                await this.PublishReleaseToClientAsync(context, packetId, message.LockToken, messageInfo, PreciseTimeSpan.FromStart);
            }
        }

        Task PublishReleaseToClientAsync(IChannelHandlerContext context, int packetId, string lockToken,
            IQos2MessageDeliveryState messageState, PreciseTimeSpan startTimestamp)
        {
            var pubRelPacket = new PubRelPacket
            {
                PacketId = packetId
            };
            return this.pubRelPubCompProcessor.SendRequestAsync(context, pubRelPacket,
                new CompletionPendingMessageState(packetId, lockToken, messageState, startTimestamp));
        }

        async Task AcknowledgePublishAsync(IChannelHandlerContext context, AckPendingMessageState message)
        {
            this.ResumeReadingIfNecessary(context);

            // todo: is try-catch needed here?
            try
            {
                await this.messagingServiceClient.CompleteAsync(message.LockToken);

                PerformanceCounters.OutboundMessageProcessingTime.Register(message.StartTimestamp);

                if (this.publishPubAckProcessor.ResumeRetransmission(context))
                {
                    return;
                }

                this.RestartReceiveIfPossible(context);
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

                await this.PublishReleaseToClientAsync(context, message.PacketId, message.LockToken, messageInfo, message.StartTimestamp);

                if (this.publishPubRecProcessor.ResumeRetransmission(context))
                {
                    return;
                }

                this.RestartReceiveIfPossible(context);
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
                await this.messagingServiceClient.CompleteAsync(message.LockToken);

                await this.qos2StateProvider.DeleteMessageAsync(this.identity, message.PacketId, message.DeliveryState);

                PerformanceCounters.OutboundMessageProcessingTime.Register(message.StartTimestamp);

                if (this.pubRelPubCompProcessor.ResumeRetransmission(context))
                {
                    return;
                }

                this.RestartReceiveIfPossible(context);
            }
            catch (Exception ex)
            {
                ShutdownOnError(context, "-> PUBCOMP", ex);
            }
        }

        void RestartReceiveIfPossible(IChannelHandlerContext context)
        {
            // restarting receive loop if was stopped due to reaching MaxOutstandingOutboundMessageCount cap
            if (!this.IsInState(StateFlags.Receiving)
                && this.MessagePendingAckCount < this.settings.MaxPendingOutboundMessages)
            {
                this.StartReceiving(context);
            }
        }

        async void RetransmitNextPublish(IChannelHandlerContext context, AckPendingMessageState messageInfo)
        {
            bool wasReceiving = this.IsInState(StateFlags.Receiving);

            try
            {
                await this.messagingServiceClient.AbandonAsync(messageInfo.LockToken);

                if (!wasReceiving)
                {
                    this.Receive(context);
                }
            }
            catch (MessagingException ex)
            {
                this.ShutdownOnReceiveError(context, ex.ToString());
            }
            catch (Exception ex)
            {
                ShutdownOnError(context, ex.ToString());
            }
        }

        async void RetransmitPublishMessage(IChannelHandlerContext context, IMessage message, AckPendingMessageState messageInfo)
        {
            try
            {
                using (message)
                {
                    string topicName;
                    message.Properties[TemplateParameters.DeviceIdTemplateParam] = this.DeviceId;
                    if (!this.messageRouter.TryRouteOutgoingMessage(RouteSourceType.Notification, message, out topicName))
                    {
                        throw new InvalidOperationException("Route mapping failed on retransmission.");
                    }

                    PublishPacket packet = await Util.ComposePublishPacketAsync(context, message, messageInfo.QualityOfService, topicName, context.Channel.Allocator);

                    messageInfo.ResetMessage(message);
                    await this.publishPubAckProcessor.RetransmitAsync(context, packet, messageInfo);
                }
            }
            catch (Exception ex)
            {
                // todo: log more details
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

        async void ShutdownOnReceiveError(IChannelHandlerContext context, string exception)
        {
            this.publishPubAckProcessor.Abort();
            this.publishProcessor.Abort();
            this.publishPubRecProcessor.Abort();
            this.pubRelPubCompProcessor.Abort();

            IMessagingServiceClient hub = this.messagingServiceClient;
            if (hub != null)
            {
                this.messagingServiceClient = null;
                try
                {
                    await hub.DisposeAsync();
                }
                catch (Exception ex)
                {
                    MqttIotHubAdapterEventSource.Log.Info("Failed to close IoT Hub Client cleanly.", ex.ToString());
                }
            }
            ShutdownOnError(context, "Receive", exception);
        }

        #endregion

        #region CONNECT handling and lifecycle management

        /// <summary>
        ///     Performs complete initialization of <see cref="MqttIotHubAdapter" /> based on received CONNECT packet.
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

                this.messagingServiceClient = await this.messagingFactory.CreateIotHubClientAsync(this.identity);

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
                        if (MqttIotHubAdapterEventSource.Log.IsVerboseEnabled)
                        {
                            MqttIotHubAdapterEventSource.Log.Verbose("Error sending 'Server Unavailable' CONNACK.", ex.ToString());
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
                if (MqttIotHubAdapterEventSource.Log.IsVerboseEnabled)
                {
                    MqttIotHubAdapterEventSource.Log.Verbose($"Requested Keep Alive timeout is longer than the max allowed. Limiting to max value of {maxTimeout.Value}.", null);
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
            MqttIotHubAdapterEventSource.Log.Verbose("Connection established.", this.identity.ToString());

            if (this.keepAliveTimeout > TimeSpan.Zero)
            {
                CheckKeepAlive(context);
            }

            this.stateFlags = StateFlags.Connected;

            this.StartReceiving(context);

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
            var handler = (MqttIotHubAdapter)context.Handler;
            if (handler.IsInState(StateFlags.WaitingForConnect))
            {
                ShutdownOnError(context, "Connection timed out on waiting for CONNECT packet from client.");
            }
        }

        static void CheckKeepAlive(object ctx)
        {
            var context = (IChannelHandlerContext)ctx;
            var self = (MqttIotHubAdapter)context.Handler;
            TimeSpan elapsedSinceLastActive = DateTime.UtcNow - self.lastClientActivityTime;
            if (elapsedSinceLastActive > self.keepAliveTimeout)
            {
                ShutdownOnError(context, "Keep Alive timed out.");
                return;
            }

            context.Channel.EventLoop.ScheduleAsync(CheckKeepAliveCallback, context, self.keepAliveTimeout - elapsedSinceLastActive);
        }

        static void ShutdownOnError(IChannelHandlerContext context, string scope, Exception exception)
        {
            ShutdownOnError(context, scope, exception.ToString());
        }

        static void ShutdownOnError(IChannelHandlerContext context, string scope, string exception)
        {
            ShutdownOnError(context, $"Exception occured ({scope}): {exception}");
        }

        /// <summary>
        ///     Logs error and initiates closure of both channel and hub connection.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="reason">Explanation for channel closure.</param>
        static void ShutdownOnError(IChannelHandlerContext context, string reason)
        {
            Contract.Requires(!string.IsNullOrEmpty(reason));

            var self = (MqttIotHubAdapter)context.Handler;
            if (!self.IsInState(StateFlags.Closed))
            {
                PerformanceCounters.ConnectionFailedOperationalPerSecond.Increment();
                MqttIotHubAdapterEventSource.Log.Warning($"Closing connection: {self.identity}", reason);
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
                MqttIotHubAdapterEventSource.Log.Warning("Error occurred while shutting down the channel.", ex);
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
                this.publishProcessor.Complete();
                this.publishPubAckProcessor.Complete();
                this.publishPubRecProcessor.Complete();
                this.pubRelPubCompProcessor.Complete();
                await Task.WhenAll(
                    this.CompletePublishAsync(context, will),
                    this.publishPubAckProcessor.Completion,
                    this.publishPubRecProcessor.Completion,
                    this.pubRelPubCompProcessor.Completion);

                IMessagingServiceClient hub = this.messagingServiceClient;
                this.messagingServiceClient = null;
                await hub.DisposeAsync();
            }
            catch (Exception ex)
            {
                MqttIotHubAdapterEventSource.Log.Info("Failed to close IoT Hub Client cleanly.", ex.ToString());
            }
        }

        async Task CompletePublishAsync(IChannelHandlerContext context, PublishPacket will)
        {
            await this.publishProcessor.Completion;

            if (will == null)
            {
                return;
            }

            try
            {
                await this.PublishToServerAsync(context, will, MessageTypes.Will);
            }
            catch (Exception ex)
            {
                MqttIotHubAdapterEventSource.Log.Warning("Failed sending Will Message.", ex);
            }
        }

        #endregion

        #region helper methods

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
                    MqttIotHubAdapterEventSource.Log.Error($"{scope}: exception occurred", task.Exception);
                }
            };
        }

        void StartReceiving(IChannelHandlerContext context)
        {
            this.stateFlags |= StateFlags.Receiving;
            this.Receive(context);
        }

        bool IsInState(StateFlags stateFlagsToCheck)
        {
            return (this.stateFlags & stateFlagsToCheck) == stateFlagsToCheck;
        }

        void ResetState(StateFlags stateFlagsToReset)
        {
            this.stateFlags &= ~stateFlagsToReset;
        }

        #endregion

        [Flags]
        enum StateFlags
        {
            WaitingForConnect = 1,
            ProcessingConnect = 1 << 1,
            Connected = 1 << 2,
            ChangingSubscriptions = 1 << 3,
            Receiving = 1 << 4,
            Closed = 1 << 5
        }
    }
}