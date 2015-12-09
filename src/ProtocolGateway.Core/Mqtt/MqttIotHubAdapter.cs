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
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Exceptions;
    using Microsoft.Azure.Devices.ProtocolGateway.Extensions;
    using Microsoft.Azure.Devices.ProtocolGateway.Instrumentation;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Auth;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Persistence;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt.Routing;

    public sealed class MqttIotHubAdapter : ChannelHandlerAdapter
    {
        const string UnmatchedFlagPropertyName = "Unmatched";
        const string SubjectPropertyName = "Subject";
        const string DeviceIdParam = "deviceId";

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
        IDeviceClient iotHubClient;
        DateTime lastClientActivityTime;
        ISessionState sessionState;
        readonly PacketAsyncProcessor<PublishPacket> publishProcessor;
        readonly RequestAckPairProcessor<AckPendingMessageState, PublishPacket> publishPubAckProcessor;
        readonly RequestAckPairProcessor<AckPendingMessageState, PublishPacket> publishPubRecProcessor;
        readonly RequestAckPairProcessor<CompletionPendingMessageState, PubRelPacket> pubRelPubCompProcessor;
        readonly ITopicNameRouter topicNameRouter;
        Dictionary<string, string> sessionContext;
        Identity identity;
        readonly IQos2StatePersistenceProvider qos2StateProvider;
        readonly QualityOfService maxSupportedQosToClient;
        TimeSpan keepAliveTimeout;
        Queue<Packet> subscriptionChangeQueue; // queue of SUBSCRIBE and UNSUBSCRIBE packets
        readonly DeviceClientFactoryFunc deviceClientFactory;
        readonly ISessionStatePersistenceProvider sessionStateManager;
        readonly IAuthenticationProvider authProvider;
        Queue<Packet> connectPendingQueue;
        PublishPacket willPacket;

        public MqttIotHubAdapter(Settings settings, DeviceClientFactoryFunc deviceClientFactory, ISessionStatePersistenceProvider sessionStateManager, IAuthenticationProvider authProvider,
            ITopicNameRouter topicNameRouter, IQos2StatePersistenceProvider qos2StateProvider)
        {
            Contract.Requires(settings != null);
            Contract.Requires(sessionStateManager != null);
            Contract.Requires(authProvider != null);
            Contract.Requires(topicNameRouter != null);

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
            this.deviceClientFactory = deviceClientFactory;
            this.sessionStateManager = sessionStateManager;
            this.authProvider = authProvider;
            this.topicNameRouter = topicNameRouter;

            this.publishProcessor = new PacketAsyncProcessor<PublishPacket>(this.PublishToServerAsync);
            this.publishProcessor.Completion.OnFault(ShutdownOnPublishToServerFaultAction);

            TimeSpan? ackTimeout = this.settings.DeviceReceiveAckCanTimeout ? this.settings.DeviceReceiveAckTimeout : (TimeSpan?)null;
            this.publishPubAckProcessor = new RequestAckPairProcessor<AckPendingMessageState, PublishPacket>(this.AcknowledgePublishAsync, this.RetransmitNextPublish, ackTimeout);
            this.publishPubAckProcessor.Completion.OnFault(ShutdownOnPubAckFaultAction);
            this.publishPubRecProcessor = new RequestAckPairProcessor<AckPendingMessageState, PublishPacket>(this.AcknowledgePublishReceiveAsync, this.RetransmitNextPublish, ackTimeout);
            this.publishPubRecProcessor.Completion.OnFault(ShutdownOnPubRecFaultAction);
            this.pubRelPubCompProcessor = new RequestAckPairProcessor<CompletionPendingMessageState, PubRelPacket>(this.AcknowledgePublishCompleteAsync, this.RetransmitNextPublishRelease, ackTimeout);
            this.pubRelPubCompProcessor.Completion.OnFault(ShutdownOnPubCompFaultAction);
        }

        Queue<Packet> SubscriptionChangeQueue
        {
            get { return this.subscriptionChangeQueue ?? (this.subscriptionChangeQueue = new Queue<Packet>(4)); }
        }

        Queue<Packet> ConnectPendingQueue
        {
            get { return this.connectPendingQueue ?? (this.connectPendingQueue = new Queue<Packet>(4)); }
        }

        bool ConnectedToHub
        {
            get { return this.iotHubClient != null; }
        }

        int InboundBacklogSize
        {
            get
            {
                return this.publishProcessor.BacklogSize
                    + this.publishPubAckProcessor.BacklogSize
                    + this.publishPubRecProcessor.BacklogSize
                    + this.pubRelPubCompProcessor.BacklogSize;
            }
        }

        int MessagePendingAckCount
        {
            get
            {
                return this.publishPubAckProcessor.RequestPendingAckCount
                    + this.publishPubRecProcessor.RequestPendingAckCount
                    + this.pubRelPubCompProcessor.RequestPendingAckCount;
            }
        }

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
                MqttIotHubAdapterEventSource.Log.Warning(string.Format("Unexpected message (only `{0}` descendants are supported): {1}", typeof(Packet).FullName, message));
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
                    ShutdownOnError(context, string.Format("First packet in the session must be CONNECT. Observed: {0}", packet));
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
                        string.Format("deviceId: {0}, messages queued: {1}, channel: {2}", this.identity, inboundBacklogSize, context.Channel));
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
                MqttIotHubAdapterEventSource.Log.Warning(string.Format("Message was received after channel closure: {0}", packet));
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
                    ShutdownOnError(context, string.Format("Packet of unsupported type was observed: {0}", packet));
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
                        await this.sessionStateManager.SetAsync(this.identity.ToString(), newState);
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

        async Task PublishToServerAsync(IChannelHandlerContext context, PublishPacket packet)
        {
            if (!this.ConnectedToHub)
            {
                return;
            }

            PreciseTimeSpan startedTimestamp = PreciseTimeSpan.FromStart;

            this.ResumeReadingIfNecessary(context);

            using (Stream bodyStream = packet.Payload.IsReadable() ? new ReadOnlyByteBufferStream(packet.Payload, true) : null)
            {
                var message = new Message(bodyStream);
                this.ApplyRoutingConfiguration(message, packet);

                Util.CompleteMessageFromPacket(message, packet, this.settings);

                await this.iotHubClient.SendAsync(message);

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
                    MqttIotHubAdapterEventSource.Log.Verbose("Resuming reading from channel as queue freed up.", string.Format("deviceId: {0}, channel: {1}", this.identity, context.Channel));
                }
                context.Read();
            }
        }

        void ApplyRoutingConfiguration(Message message, PublishPacket packet)
        {
            RouteDestinationType routeType;
            if (this.topicNameRouter.TryMapTopicNameToRoute(packet.TopicName, out routeType, message.Properties))
            {
                // successfully matched topic against configured routes -> validate topic name
                string messageDeviceId;
                if (message.Properties.TryGetValue(DeviceIdParam, out messageDeviceId))
                {
                    if (!this.identity.DeviceId.Equals(messageDeviceId, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            string.Format("Device ID provided in topic name ({0}) does not match ID of the device publishing message ({1}). IoT Hub Name: {2}",
                            messageDeviceId, this.identity.DeviceId, this.identity.IoTHubHostName));
                    }
                    message.Properties.Remove(DeviceIdParam);
                }
            }
            else
            {
                if (MqttIotHubAdapterEventSource.Log.IsWarningEnabled)
                {
                    MqttIotHubAdapterEventSource.Log.Warning("Topic name could not be matched against any of the configured routes. Falling back to default telemetry settings.", packet.ToString());
                }
                routeType = RouteDestinationType.Telemetry;
                message.Properties[UnmatchedFlagPropertyName] = bool.TrueString;
                message.Properties[SubjectPropertyName] = packet.TopicName;
            }

            // once we have different routes, this will change to tackle different aspects of route types
            switch (routeType)
            {
                case RouteDestinationType.Telemetry:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(string.Format("Unexpected route type: {0}", routeType));
            }
        }

        #endregion

        #region PUBLISH Server -> Client handling

        async void Receive(IChannelHandlerContext context)
        {
            Contract.Requires(this.sessionContext != null);

            try
            {
                Message message = await this.iotHubClient.ReceiveAsync();
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
                    if (pendingPubAck.MessageId.Equals(message.MessageId, StringComparison.Ordinal))
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
                        if (pendingPubRec.MessageId.Equals(message.MessageId, StringComparison.Ordinal))
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
            catch (IotHubException ex)
            {
                this.ShutdownOnReceiveError(context, ex.ToString());
            }
            catch (Exception ex)
            {
                ShutdownOnError(context, "Receive", ex.ToString());
            }
        }

        async Task PublishToClientAsync(IChannelHandlerContext context, Message message)
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
                    var completeContext = new ReadOnlyMergeDictionary<string, string>(this.sessionContext, message.Properties);
                    if (!this.topicNameRouter.TryMapRouteToTopicName(RouteSourceType.Notification, completeContext, out topicName))
                    {
                        // source is not configured
                        await this.RejectMessageAsync(message);
                        return;
                    }

                    QualityOfService qos;
                    QualityOfService maxRequestedQos;
                    if (this.TryMatchSubscription(topicName, message.EnqueuedTimeUtc, out maxRequestedQos))
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

                    PublishPacket packet = await Util.ComposePublishPacketAsync(context, message, qos, topicName);
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

        async Task RejectMessageAsync(Message message)
        {
            await this.iotHubClient.RejectAsync(message.LockToken); // awaiting guarantees that we won't complete consecutive message before this is completed.
            PerformanceCounters.MessagesRejectedPerSecond.Increment();
        }

        Task PublishToClientQos0Async(IChannelHandlerContext context, Message message, PublishPacket packet)
        {
            if (message.DeliveryCount == 0)
            {
                return Task.WhenAll(
                    this.iotHubClient.CompleteAsync(message.LockToken),
                    Util.WriteMessageAsync(context, packet));
            }
            else
            {
                return this.iotHubClient.CompleteAsync(message.LockToken);
            }
        }

        Task PublishToClientQos1Async(IChannelHandlerContext context, Message message, PublishPacket packet)
        {
            return this.publishPubAckProcessor.SendRequestAsync(context, packet, new AckPendingMessageState(message, packet));
        }

        async Task PublishToClientQos2Async(IChannelHandlerContext context, Message message, PublishPacket packet)
        {
            int packetId = packet.PacketId;
            IQos2MessageDeliveryState messageInfo = await this.qos2StateProvider.GetMessageAsync(packetId);

            if (messageInfo != null && !message.MessageId.Equals(messageInfo.MessageId, StringComparison.Ordinal))
            {
                await this.qos2StateProvider.DeleteMessageAsync(packetId, messageInfo);
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
                await this.iotHubClient.CompleteAsync(message.LockToken);

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
                IQos2MessageDeliveryState messageInfo = this.qos2StateProvider.Create(message.MessageId);
                await this.qos2StateProvider.SetMessageAsync(message.PacketId, messageInfo);

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
                await this.iotHubClient.CompleteAsync(message.LockToken);

                await this.qos2StateProvider.DeleteMessageAsync(message.PacketId, message.DeliveryState);

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
                await this.iotHubClient.AbandonAsync(messageInfo.LockToken);

                if (!wasReceiving)
                {
                    this.Receive(context);
                }
            }
            catch (IotHubException ex)
            {
                this.ShutdownOnReceiveError(context, ex.ToString());
            }
            catch (Exception ex)
            {
                ShutdownOnError(context, ex.ToString());
            }
        }

        async void RetransmitPublishMessage(IChannelHandlerContext context, Message message, AckPendingMessageState messageInfo)
        {
            try
            {
                using (message)
                {
                    string topicName;
                    var completeContext = new ReadOnlyMergeDictionary<string, string>(this.sessionContext, message.Properties);
                    if (!this.topicNameRouter.TryMapRouteToTopicName(RouteSourceType.Notification, completeContext, out topicName))
                    {
                        throw new InvalidOperationException("Route mapping failed on retransmission.");
                    }

                    PublishPacket packet = await Util.ComposePublishPacketAsync(context, message, messageInfo.QualityOfService, topicName);

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
            foreach (Subscription subscription in this.sessionState.Subscriptions)
            {
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

            IDeviceClient hub = this.iotHubClient;
            if (hub != null)
            {
                this.iotHubClient = null;
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
                AuthenticationResult authResult = await this.authProvider.AuthenticateAsync(packet.ClientId,
                    packet.Username, packet.Password, context.Channel.RemoteAddress);
                if (authResult == null)
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

                this.identity = authResult.Identity;

                this.iotHubClient = await this.deviceClientFactory(authResult);

                bool sessionPresent = await this.EstablishSessionStateAsync(this.identity.ToString(), packet.CleanSession);

                this.keepAliveTimeout = this.DeriveKeepAliveTimeout(packet);

                if (packet.HasWill)
                {
                    var will = new PublishPacket(packet.WillQualityOfService, false, packet.WillRetain);
                    will.TopicName = packet.WillTopicName;
                    will.Payload = packet.WillMessage;
                    this.willPacket = will;
                }

                this.sessionContext = new Dictionary<string, string>
                {
                    { DeviceIdParam, this.identity.DeviceId }
                };

                this.StartReceiving(context);

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
        /// <param name="clientId">Client identificator to load the session state for.</param>
        /// <param name="cleanSession">Determines whether session has to be deleted if it already exists.</param>
        /// <returns></returns>
        async Task<bool> EstablishSessionStateAsync(string clientId, bool cleanSession)
        {
            ISessionState existingSessionState = await this.sessionStateManager.GetAsync(clientId);
            if (cleanSession)
            {
                if (existingSessionState != null)
                {
                    await this.sessionStateManager.DeleteAsync(clientId, existingSessionState);
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
                    MqttIotHubAdapterEventSource.Log.Verbose(string.Format("Requested Keep Alive timeout is longer than the max allowed. Limiting to max value of {0}.", maxTimeout.Value), null);
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
            ShutdownOnError(context, string.Format("Exception occured ({0}): {1}", scope, exception));
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
                MqttIotHubAdapterEventSource.Log.Warning(string.Format("Closing connection ({0}, {1}): {2}", context.Channel.RemoteAddress, self.identity, reason));
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
                if (will != null)
                {
                    // try publishing will message before shutting down IoT Hub connection
                    try
                    {
                        this.publishProcessor.Post(context, will);
                    }
                    catch (Exception ex)
                    {
                        MqttIotHubAdapterEventSource.Log.Warning("Failed sending Will Message.", ex);
                    }
                }

                this.CloseIotHubConnection();
                await context.CloseAsync();
            }
            catch (Exception ex)
            {
                MqttIotHubAdapterEventSource.Log.Warning("Error occurred while shutting down the channel.", ex);
            }
        }

        async void CloseIotHubConnection()
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
                    this.publishProcessor.Completion,
                    this.publishPubAckProcessor.Completion,
                    this.publishPubRecProcessor.Completion,
                    this.pubRelPubCompProcessor.Completion);

                IDeviceClient hub = this.iotHubClient;
                this.iotHubClient = null;
                await hub.DisposeAsync();
            }
            catch (Exception ex)
            {
                MqttIotHubAdapterEventSource.Log.Info("Failed to close IoT Hub Client cleanly.", ex.ToString());
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
                    MqttIotHubAdapterEventSource.Log.Error(string.Format("{0}: exception occurred", scope), task.Exception);
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