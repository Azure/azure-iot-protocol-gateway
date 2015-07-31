// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Core.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.IO;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Gateway.Core.Extensions;

    public sealed class BridgeDriver : ChannelHandlerAdapter
    {
        const QualityOfService MaxSupportedQoS = QualityOfService.AtLeastOnce;
        const string UnmatchedFlagPropertyName = "Unmatched";
        const string SubjectPropertyName = "Subject";
        const string DeviceIdParam = "deviceId";

        static readonly Action<object> CheckConnectTimeoutCallback = CheckConnectionTimeout;
        static readonly Action<object> CheckKeepAliveCallback = CheckKeepAlive;
        static readonly Action<object> CheckRetransmissionNeededCallback = CheckRetransmissionNeeded;
        static readonly Action<Task, object> ShutdownOnWriteFaultAction = (task, ctx) => ShutdownOnError((IChannelHandlerContext)ctx, "WriteAndFlushAsync", task.Exception);
        static readonly Action<Task> ShutdownOnPublishFaultAction = CreateScopedFaultAction("-> PUBLISH");
        static readonly Action<Task> ShutdownOnPubAckFaultAction = CreateScopedFaultAction("-> PUBACK");

        readonly Settings settings;
        StateFlags stateFlags;
        DateTime lastClientActivityTime;
        ISessionState sessionState;
        DeviceClient iotHubClient;
        readonly AsyncChannelMessageProcessor<PublishPacket> publishProcessor;
        readonly AsyncChannelMessageProcessor<PubAckPacket> pubAckProcessor;
        Queue<AckPendingState> ackPendingQueue;
        readonly ITopicNameRouter topicNameRouter;
        Dictionary<string, string> sessionContext;
        string deviceId;
        TimeSpan keepAliveTimeout;
        Queue<Packet> subscriptionChangeQueue; // queue of SUBSCRIBE and UNSUBSCRIBE packets
        readonly ISessionStateManager sessionStateManager;
        readonly IAuthenticationProvider authProvider;
        Queue<Packet> connectPendingQueue;
        IByteBuffer willMessage;
        QualityOfService willQoS;

        public BridgeDriver(Settings settings, ISessionStateManager sessionStateManager, IAuthenticationProvider authProvider, ITopicNameRouter topicNameRouter)
        {
            Contract.Requires(settings != null);
            Contract.Requires(sessionStateManager != null);
            Contract.Requires(authProvider != null);

            this.settings = settings;
            this.sessionStateManager = sessionStateManager;
            this.authProvider = authProvider;
            this.topicNameRouter = topicNameRouter;

            this.pubAckProcessor = new AsyncChannelMessageProcessor<PubAckPacket>(this.ProcessPubAckAsync);
            this.pubAckProcessor.Completion.OnFault(ShutdownOnPubAckFaultAction);
            this.publishProcessor = new AsyncChannelMessageProcessor<PublishPacket>(this.ProcessInboundPublishAsync);
            this.publishProcessor.Completion.OnFault(ShutdownOnPublishFaultAction);
        }

        Queue<AckPendingState> AckPendingQueue
        {
            get { return this.ackPendingQueue ?? (this.ackPendingQueue = new Queue<AckPendingState>(4)); }
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

        int CalculateInboundBacklogSize()
        {
            return this.pubAckProcessor.Count + this.publishProcessor.Count;
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            this.stateFlags = StateFlags.WaitingForConnect;
            TimeSpan? timeout = this.settings.ConnectArrivalTimeout;
            if (timeout.HasValue)
            {
                context.Channel.EventLoop.Schedule(CheckConnectTimeoutCallback, context, timeout.Value);
            }
            base.ChannelActive(context);

            context.Read();
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            var packet = message as Packet;
            if (packet == null)
            {
                BridgeEventSource.Log.Warning(string.Format("Unexpected message (only `{0}` descendants are supported): {1}", typeof(Packet).FullName, message));
                return;
            }

            this.lastClientActivityTime = DateTime.UtcNow;
            if (!this.BufferIfNotConnected(context, packet))
            {
                this.ProcessMessage(context, packet);
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            base.ChannelReadComplete(context);
            if (this.CalculateInboundBacklogSize() < this.settings.MaxOutstandingInboundMessages)
            {
                context.Read();
            }
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            this.ShutdownAsync(context, false);

            base.ChannelInactive(context);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            ShutdownOnError(context, "Exception encountered: " + exception);
        }

        void ProcessMessage(IChannelHandlerContext context, Packet packet)
        {
            if (this.IsInState(StateFlags.Closed))
            {
                BridgeEventSource.Log.Warning(string.Format("Message was received after channel closure: {0}", packet));
                return;
            }

            switch (packet.PacketType)
            {
                case PacketType.CONNECT:
                    this.OnConnectAsync(context, (ConnectPacket)packet);
                    break;
                case PacketType.PUBLISH:
                    this.publishProcessor.Post(context, (PublishPacket)packet);
                    break;
                case PacketType.PUBACK:
                    this.pubAckProcessor.Post(context, (PubAckPacket)packet);
                    break;
                case PacketType.SUBSCRIBE:
                case PacketType.UNSUBSCRIBE:
                    this.OnSubscriptionChange(context, packet);
                    break;
                case PacketType.PINGREQ:
                    // no further action is needed - keep-alive "timer" was reset by now
                    context.WriteAndFlushAsync(PingRespPacket.Instance)
                        .OnFault(ShutdownOnWriteFaultAction, context);
                    break;
                case PacketType.DISCONNECT:
                    BridgeEventSource.Log.Verbose("Disconnecting gracefully.", this.deviceId);
                    this.ShutdownAsync(context, true);
                    break;
                default:
                    ShutdownOnError(context, string.Format("Packet of unsupported type was observed: {0}", packet));
                    break;
            }
        }

        #region SUBSCRIBE/UNSUBSCRIBE

        void OnSubscriptionChange(IChannelHandlerContext context, Packet packet)
        {
            this.SubscriptionChangeQueue.Enqueue(packet);

            if (!this.IsInState(StateFlags.ChangingSubscriptions))
            {
                this.stateFlags |= StateFlags.ChangingSubscriptions;
                this.ProcessPendingSubscriptionChangesAsync(context);
            }
        }

        async void ProcessPendingSubscriptionChangesAsync(IChannelHandlerContext context)
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
                                acks.Add(AddSubscriptions(newState, (SubscribePacket)packet));
                                break;
                            case PacketType.UNSUBSCRIBE:
                                acks.Add(RemoveSubscriptions(newState, (UnsubscribePacket)packet));
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                    queue.Clear();

                    if (!this.sessionState.IsTransient)
                    {
                        // save updated session state, make it current once successfully set
                        await this.sessionStateManager.SetAsync(this.deviceId, newState);
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
                }
                while (this.subscriptionChangeQueue.Count > 0);

                this.ResetState(StateFlags.ChangingSubscriptions);
            }
            catch (Exception ex)
            {
                ShutdownOnError(context, "-> UN/SUBSCRIBE", ex);
            }
        }

        static SubAckPacket AddSubscriptions(ISessionState session, SubscribePacket packet)
        {
            List<Subscription> subscriptions = session.Subscriptions;
            var returnCodes = new List<QualityOfService>(subscriptions.Count);
            foreach (SubscriptionRequest request in packet.Requests)
            {
                Subscription existingSubscription = null;
                for (int i = subscriptions.Count - 1; i >= 0; i--)
                {
                    Subscription subscription = subscriptions[i];
                    if (subscription.TopicFilter.Equals(request.TopicFilter, StringComparison.Ordinal))
                    {
                        subscriptions.RemoveAt(i);
                        existingSubscription = subscription;
                        break;
                    }
                }

                QualityOfService finalQos = request.QualityOfService < MaxSupportedQoS ? request.QualityOfService : MaxSupportedQoS;

                subscriptions.Add(existingSubscription == null
                    ? new Subscription(request.TopicFilter, request.QualityOfService)
                    : existingSubscription.CreateUpdated(finalQos));

                returnCodes.Add(finalQos);
            }
            var ack = new SubAckPacket
            {
                PacketId = packet.PacketId,
                ReturnCodes = returnCodes
            };
            return ack;
        }

        static UnsubAckPacket RemoveSubscriptions(ISessionState session, UnsubscribePacket packet)
        {
            List<Subscription> subscriptions = session.Subscriptions;
            foreach (string topicToRemove in packet.TopicFilters)
            {
                for (int i = subscriptions.Count - 1; i >= 0; i--)
                {
                    if (subscriptions[i].TopicFilter.Equals(topicToRemove, StringComparison.Ordinal))
                    {
                        subscriptions.RemoveAt(i);
                        break;
                    }
                }
            }
            var ack = new UnsubAckPacket
            {
                PacketId = packet.PacketId
            };
            return ack;
        }

        #endregion

        #region PUBLISH Client->Server

        async Task ProcessInboundPublishAsync(IChannelHandlerContext context, PublishPacket packet)
        {
            if (!this.ConnectedToHub)
            {
                return;
            }

            this.ResumeReadingIfNecessary(context);

            using (Stream bodyStream = packet.Payload.IsReadable() ? new ReadOnlyByteBufferStream(packet.Payload, true) : null)
            {
                var message = new Message(bodyStream);
                this.ApplyMessageRoutingConfiguration(message, packet);

                Util.CompleteMessageFromPacket(message, packet, this.settings);

                await this.iotHubClient.SendEventAsync(message);
            }

            if (!this.IsInState(StateFlags.Closed))
            {
                switch (packet.QualityOfService)
                {
                    case QualityOfService.AtMostOnce:
                        // no response necessary
                        break;
                    case QualityOfService.AtLeastOnce:
                        context.WriteAndFlushAsync(PubAckPacket.InResponseTo(packet))
                            .OnFault(ShutdownOnWriteFaultAction, context);
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
            if (this.CalculateInboundBacklogSize() == this.settings.MaxOutstandingInboundMessages - 1) // we picked up a packet from full queue - now we have more room so order another read
            {
                context.Read();
            }
        }

        void ApplyMessageRoutingConfiguration(Message message, PublishPacket packet)
        {
            RouteDestinationType routeType;
            if (this.topicNameRouter.TryMapTopicNameToRoute(packet.TopicName, out routeType, message.Properties))
            {
                // successfully matched topic against configured routes -> validate topic name
                string messageDeviceId;
                if (message.Properties.TryGetValue(DeviceIdParam, out messageDeviceId))
                {
                    if (!this.deviceId.Equals(messageDeviceId, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(string.Format("Device ID provided in topic name ({0}) does not match ID of the device publishing message ({1}).",
                            messageDeviceId,
                            this.deviceId));
                    }
                    message.Properties.Remove(DeviceIdParam);
                }
            }
            else
            {
                if (BridgeEventSource.Log.IsWarningEnabled)
                {
                    BridgeEventSource.Log.Warning("Topic name could not be matched against any of the configured routes. Falling back to default telemetry settings.", packet.ToString());
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

        #region PUBLISH Server->Client

        async void ReceiveOutboundAsync(IChannelHandlerContext context)
        {
            Contract.Assert(!this.IsInState(StateFlags.Receiving));
            Contract.Assert(this.sessionContext != null);
            this.stateFlags |= StateFlags.Receiving;

            try
            {
                while (!this.IsInAnyState(StateFlags.Retransmitting | StateFlags.Closed)
                    && (this.ackPendingQueue == null || this.ackPendingQueue.Count < this.settings.MaxOutstandingOutboundMessages))
                {
                    Message message = await this.iotHubClient.ReceiveAsync(TimeSpan.MaxValue);
                    if (message == null)
                    {
                        // link to IoT Hub has been closed
                        this.ShutdownOnReceiveErrorAsync(context, null);
                        return;
                    }

                    this.PublishMessageAsync(context, message);
                }
            }
            catch (Exception ex)
            {
                this.ShutdownOnReceiveErrorAsync(context, ex.ToString());
            }

            this.ResetState(StateFlags.Receiving);
        }

        async void PublishMessageAsync(IChannelHandlerContext context, Message message)
        {
            try
            {
                using (message)
                {
                    // todo: check delivery count
                    // todo: disable device if same message is not ACKed X times (based on message.DeliveryCount in IoT Hub)?
                    // todo: or deadletter the message?

                    string topicName;
                    QualityOfService maxRequestedQoS;
                    if (!this.TryDeriveMessageTopicName(message, out topicName)
                        || !this.TryMatchSubscription(topicName, message.EnqueuedTimeUtc, out maxRequestedQoS))
                    {
                        // source is not configured or
                        // no matching subscription found - complete the message without publishing
                        await this.iotHubClient.RejectAsync(message.LockToken); // awaiting guarantees that we won't complete consecutive message before this is completed.
                        return;
                    }

                    QualityOfService qos = Util.DeriveQoS(message, this.settings);
                    if (maxRequestedQoS < qos)
                    {
                        qos = maxRequestedQoS;
                    }

                    PublishPacket packet = await Util.ComposePublishPacketAsync(context, message, qos, topicName);
                    switch (qos)
                    {
                        case QualityOfService.AtMostOnce:
                            await this.PublishToClientQoS0Async(context, message, packet);
                            break;
                        case QualityOfService.AtLeastOnce:
                            await this.PublishToClientQoS1Async(context, message, packet);
                            break;
                        default:
                            throw new InvalidOperationException("QoS cannot be provided.");
                    }
                }
            }
            catch (Exception ex)
            {
                // todo: log more details
                ShutdownOnError(context, "<- PUBLISH", ex);
            }
        }

        bool TryDeriveMessageTopicName(Message message, out string topicName)
        {
            return this.topicNameRouter.TryMapRouteToTopicName(
                RouteSourceType.Notification,
                new ReadOnlyMergeDictionary<string, string>(this.sessionContext, message.Properties),
                out topicName);
        }

        async Task ProcessPubAckAsync(IChannelHandlerContext context, PubAckPacket packet)
        {
            this.ResumeReadingIfNecessary(context);

            Queue<AckPendingState> queue = this.ackPendingQueue;
            if (queue == null || queue.Count == 0)
            {
                return;
            }

            AckPendingState pendingPacketInfo = queue.Peek();
            if (packet.PacketId != pendingPacketInfo.PacketId)
            {
                if (BridgeEventSource.Log.IsWarningEnabled)
                {
                    BridgeEventSource.Log.Warning(string.Format("PUBACK #{0} received while #{1} was expected.", packet.PacketId, pendingPacketInfo.PacketId));
                }
                return;
            }

            Contract.Assert(queue.Dequeue() == pendingPacketInfo);

            try
            {
                await this.iotHubClient.CompleteAsync(pendingPacketInfo.LockToken);

                if (this.IsInState(StateFlags.Retransmitting))
                {
                    if (queue.Count > 0)
                    {
                        this.RetransmitOutboundFromQueueAsync(context);
                        return;
                    }
                    else
                    {
                        this.ResetState(StateFlags.Retransmitting);
                    }
                }

                // restarting receive loop if was stopped due to reaching MaxOutstandingOutboundMessageCount cap
                if (!this.IsInState(StateFlags.Receiving) && queue.Count < this.settings.MaxOutstandingOutboundMessages)
                {
                    this.ReceiveOutboundAsync(context);
                }
            }
            catch (Exception ex)
            {
                ShutdownOnError(context, "-> PUBACK", ex);
            }
        }

        async Task PublishToClientQoS0Async(IChannelHandlerContext context, Message message, PublishPacket packet)
        {
            if (message.DeliveryCount == 0)
            {
                await Task.WhenAll(
                    this.iotHubClient.CompleteAsync(message.LockToken),
                    context.WriteAndFlushAsync(packet));
            }
            else
            {
                await this.iotHubClient.CompleteAsync(message.LockToken);
            }
        }

        async Task PublishToClientQoS1Async(IChannelHandlerContext context, Message message, PublishPacket packet)
        {
            // this is safe as there is only one queue for processing messages and MaxOutstandingOutboundMessages setting cannot be more than 65535
            int packetId = unchecked((ushort)message.SequenceNumber);
            packet.PacketId = packetId;
            this.AckPendingQueue.Enqueue(
                new AckPendingState(message.MessageId, packetId, packet.QualityOfService, packet.TopicName, message.LockToken));
            if (this.IsInState(StateFlags.Retransmitting))
            {
                // retransmission is underway so the message has to be sent only after retransmission is done;
                // we cannot abandon message right now as it would mess up the order so we leave message in the queue without sending it.
                return;
            }

            if (!this.IsInState(StateFlags.RetransmissionCheckScheduled) && this.settings.DeviceReceiveAckCanTimeout)
            {
                // if retransmission is configured, schedule check for timeout if posting first message in queue
                this.ScheduleOutboundRetransmissionCheck(context, this.settings.DeviceReceiveAckTimeout);
            }

            await context.WriteAndFlushAsync(packet);
        }

        void ScheduleOutboundRetransmissionCheck(IChannelHandlerContext context, TimeSpan delay)
        {
            if (this.IsInState(StateFlags.RetransmissionCheckScheduled))
            {
                this.stateFlags |= StateFlags.RetransmissionCheckScheduled;
                context.Channel.EventLoop.Schedule(CheckRetransmissionNeededCallback, context, delay);
            }
        }

        static void CheckRetransmissionNeeded(object ctx)
        {
            var context = (IChannelHandlerContext)ctx;
            var self = (BridgeDriver)context.Handler;
            self.ResetState(StateFlags.RetransmissionCheckScheduled); // unset "retransmission check scheduled" flag
            Queue<AckPendingState> queue = self.ackPendingQueue;
            if (queue == null || queue.Count == 0 || !self.settings.DeviceReceiveAckCanTimeout)
            {
                return;
            }

            AckPendingState oldestPendingPacket = queue.Peek();
            TimeSpan timeoutLeft = self.settings.DeviceReceiveAckTimeout - (DateTime.UtcNow - oldestPendingPacket.PublishTime);
            if (timeoutLeft.Ticks <= 0)
            {
                self.stateFlags |= StateFlags.Retransmitting;
                // entering retransmission mode
                self.RetransmitOutboundFromQueueAsync(context);
            }
            else
            {
                // rescheduling check for when timeout would happen for current top message pending ack
                self.ScheduleOutboundRetransmissionCheck(context, timeoutLeft);
            }
        }

        async void RetransmitOutboundFromQueueAsync(IChannelHandlerContext context)
        {
            try
            {
                Queue<AckPendingState> queue = this.ackPendingQueue;
                Contract.Assert(queue != null);
                AckPendingState messageInfo = queue.Peek();

                await this.iotHubClient.AbandonAsync(messageInfo.LockToken);
                Message message = await this.iotHubClient.ReceiveAsync();
                if (message == null)
                {
                    // link to IoT Hub has been closed
                    this.ShutdownOnReceiveErrorAsync(context, null);
                    return;
                }

                if (message.MessageId != messageInfo.MessageId)
                {
                    // it is an indication that queue is being accessed not exclusively - terminate the connection
                    this.ShutdownOnReceiveErrorAsync(context, string.Format("Expected to receive message with id of {0} but saw a message with id of {1}. Protocol Gateway only supports exclusive connection to IoT Hub.",
                        messageInfo.MessageId, message.MessageId));
                    return;
                }

                // todo: check delivery count
                // todo: disable device if same message is not ACKed X times (based on message.DeliveryCount in IoT Hub)?
                // todo: or deadletter the message?

                PublishPacket packet = await Util.ComposePublishPacketAsync(context, message, messageInfo.QualityOfService, messageInfo.TopicName);
                packet.PacketId = messageInfo.PacketId;
                messageInfo.Reset(message.LockToken);
                await context.WriteAndFlushAsync(packet);

                if (this.settings.DeviceReceiveAckCanTimeout)
                {
                    this.ScheduleOutboundRetransmissionCheck(context, this.settings.DeviceReceiveAckTimeout);
                }
            }
            catch (Exception ex)
            {
                this.ShutdownOnReceiveErrorAsync(context, ex.ToString());
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
                    if (qos >= MaxSupportedQoS)
                    {
                        qos = MaxSupportedQoS;
                        break;
                    }
                }
            }
            return found;
        }

        async void ShutdownOnReceiveErrorAsync(IChannelHandlerContext context, string exception)
        {
            this.pubAckProcessor.Abort();
            this.publishProcessor.Abort();

            DeviceClient hub = this.iotHubClient;
            if (hub != null)
            {
                this.iotHubClient = null;
                try
                {
                    await hub.CloseAsync();
                }
                catch (Exception ex)
                {
                    BridgeEventSource.Log.Info("Failed to close IoT Hub Client cleanly.", ex.ToString());
                }
            }
            ShutdownOnError(context, "Receive", exception);
        }

        #endregion

        #region CONNECT and session management

        async void OnConnectAsync(IChannelHandlerContext context, ConnectPacket packet)
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
                if (!authResult.IsSuccessful)
                {
                    connAckSent = true;
                    await context.WriteAndFlushAsync(new ConnAckPacket
                    {
                        ReturnCode = ConnectReturnCode.RefusedNotAuthorized
                    });
                    ShutdownOnError(context, "Authentication failed.");
                    return;
                }

                this.deviceId = authResult.DeviceId;

                string connectionString = Util.ComposeIoTHubConnectionString(this.settings, authResult.Scope, authResult.KeyName, authResult.KeyValue);
                this.iotHubClient = DeviceClient.CreateFromConnectionString(connectionString, this.deviceId);
                await this.iotHubClient.OpenAsync();

                bool sessionPresent = await this.EstablishSessionStateAsync(this.deviceId, packet.CleanSession);

                if (this.sessionState.IsTransient)
                {
                    // purging queue
                    Message message;
                    while ((message = await this.iotHubClient.ReceiveAsync(TimeSpan.Zero)) != null)
                    {
                        await this.iotHubClient.RejectAsync(message);
                    }
                }

                this.keepAliveTimeout = this.DeriveKeepAliveTimeout(packet);
                if (packet.HasWill)
                {
                    this.willMessage = packet.WillMessage;
                    this.willQoS = packet.WillQualityOfService;
                }
                this.sessionContext = new Dictionary<string, string>
                {
                    { DeviceIdParam, this.deviceId }
                };

                this.ReceiveOutboundAsync(context);

                connAckSent = true;
                await context.WriteAndFlushAsync(new ConnAckPacket
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
                        await context.WriteAndFlushAsync(new ConnAckPacket
                        {
                            ReturnCode = ConnectReturnCode.RefusedServerUnavailable
                        });
                    }
                    catch (Exception ex)
                    {
                        if (BridgeEventSource.Log.IsVerboseEnabled)
                        {
                            BridgeEventSource.Log.Verbose("Error sending 'Server Unavailable' CONNACK.", ex.ToString());
                        }
                    }
                }

                ShutdownOnError(context, "CONNECT", exception);
            }
        }

        async Task<bool> EstablishSessionStateAsync(string clientId, bool cleanSession)
        {
            if (cleanSession)
            {
                ISessionState existingSessionState = await this.sessionStateManager.GetAsync(clientId);
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
                this.sessionState = await this.sessionStateManager.GetAsync(clientId);
                if (this.sessionState == null)
                {
                    this.sessionState = this.sessionStateManager.Create(false);
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        TimeSpan DeriveKeepAliveTimeout(ConnectPacket packet)
        {
            TimeSpan timeout = TimeSpan.FromSeconds(packet.KeepAliveInSeconds * 1.5);
            TimeSpan? maxTimeout = this.settings.MaxKeepAliveTimeout;
            if (maxTimeout.HasValue && (this.keepAliveTimeout > maxTimeout.Value || this.keepAliveTimeout == TimeSpan.Zero))
            {
                if (BridgeEventSource.Log.IsVerboseEnabled)
                {
                    BridgeEventSource.Log.Verbose(string.Format("Requested Keep Alive timeout is longer than the max allowed. Limiting to max value of {0}.", maxTimeout.Value), null);
                }
                return maxTimeout.Value;
            }

            return timeout;
        }

        void CompleteConnect(IChannelHandlerContext context)
        {
            BridgeEventSource.Log.Verbose("Connection established.", this.deviceId);

            if (this.keepAliveTimeout > TimeSpan.Zero)
            {
                CheckKeepAlive(context);
            }

            this.stateFlags = StateFlags.Connected;

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
            var handler = (BridgeDriver)context.Handler;
            if (handler.IsInState(StateFlags.WaitingForConnect))
            {
                ShutdownOnError(context, "Connection timed out on waiting for CONNECT packet from client.");
            }
        }

        static void CheckKeepAlive(object ctx)
        {
            var context = (IChannelHandlerContext)ctx;
            var self = (BridgeDriver)context.Handler;
            TimeSpan elapsedSinceLastActive = DateTime.UtcNow - self.lastClientActivityTime;
            if (elapsedSinceLastActive > self.keepAliveTimeout)
            {
                ShutdownOnError(context, "Keep Alive timed out.");
                return;
            }

            context.Channel.EventLoop.Schedule(CheckKeepAliveCallback, context, self.keepAliveTimeout - elapsedSinceLastActive);
        }

        static void ShutdownOnError(IChannelHandlerContext context, string scope, Exception exception)
        {
            ShutdownOnError(context, scope, exception.ToString());
        }

        static void ShutdownOnError(IChannelHandlerContext context, string scope, string exception)
        {
            ShutdownOnError(context, string.Format("Exception occured ({0}): {1}", scope, exception));
        }

        static void ShutdownOnError(IChannelHandlerContext context, string reason)
        {
            Contract.Requires(!string.IsNullOrEmpty(reason));

            var self = (BridgeDriver)context.Handler;
            if (!self.IsInState(StateFlags.Closed))
            {
                BridgeEventSource.Log.Warning(string.Format("Closing connection ({0}, {1}): {2}", context.Channel.RemoteAddress, self.deviceId, reason));
                self.ShutdownAsync(context, false);
            }
        }

        async void ShutdownAsync(IChannelHandlerContext context, bool graceful)
        {
            if (!this.IsInState(StateFlags.Closed))
            {
                this.stateFlags |= StateFlags.Closed; // "or" not to interfere with ongoing logic which has to honor Closed state when it's right time to do (case by case)
                Queue<Packet> connectQueue = this.connectPendingQueue;
                if (connectQueue != null)
                {
                    while (connectQueue.Count > 0)
                    {
                        Packet packet = connectQueue.Dequeue();
                        ReferenceCountUtil.Release(packet);
                    }
                }
                this.CloseIotHubConnectionAsync(graceful);
                await context.CloseAsync();
            }
        }

        async void CloseIotHubConnectionAsync(bool graceful)
        {
            if (!this.ConnectedToHub)
            {
                // closure happened before IoT Hub connection was established or it was initiated due to disconnect
                return;
            }

            try
            {
                this.pubAckProcessor.Complete();
                this.publishProcessor.Complete();
                await Task.WhenAll(
                    this.publishProcessor.Completion,
                    this.pubAckProcessor.Completion);

                IByteBuffer willPayload = graceful && this.IsInState(StateFlags.Connected) ? this.willMessage : null;
                if (willPayload != null)
                {
                    // try publishing will message before shutting down IoT Hub connection
                    try
                    {
                        var message = new Message(willPayload.ToArray());
                        message.Properties[this.settings.TopicNameProperty] = this.settings.WillTopicName;
                        await this.iotHubClient.SendEventAsync(message);
                    }
                    catch (Exception ex)
                    {
                        BridgeEventSource.Log.Warning("Failed sending Will Message.", ex);
                    }
                }

                DeviceClient hub = this.iotHubClient;
                this.iotHubClient = null;
                await hub.CloseAsync();
            }
            catch (Exception ex)
            {
                BridgeEventSource.Log.Info("Failed to close IoT Hub Client cleanly.", ex.ToString());
            }
        }

        bool BufferIfNotConnected(IChannelHandlerContext context, Packet packet)
        {
            if (this.IsInState(StateFlags.Connected))
            {
                return false;
            }

            if (packet.PacketType == PacketType.CONNECT)
            {
                return false;
            }

            if (!this.IsInState(StateFlags.ProcessingConnect))
            {
                ShutdownOnError(context, "First packet in the session must be CONNECT. Observed: " + packet);
                return true;
            }

            this.ConnectPendingQueue.Enqueue(packet);
            return true;
        }

        #endregion

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
                    BridgeEventSource.Log.Error(string.Format("{0}: Unexpected exception", scope), task.Exception);
                }
            };
        }

        bool IsInState(StateFlags stateFlagsToCheck)
        {
            return (this.stateFlags & stateFlagsToCheck) == stateFlagsToCheck;
        }

        bool IsInAnyState(StateFlags stateFlagsToCheck)
        {
            return (this.stateFlags & stateFlagsToCheck) != 0;
        }

        void ResetState(StateFlags stateFlagsToReset)
        {
            this.stateFlags &= ~stateFlagsToReset;
        }

        [Flags]
        enum StateFlags
        {
            WaitingForConnect = 1,
            ProcessingConnect = 1 << 1,
            Connected = 1 << 2,
            ChangingSubscriptions = 1 << 3,
            Receiving = 1 << 4,
            RetransmissionCheckScheduled = 1 << 5,
            Retransmitting = 1 << 6,
            Closed = 1 << 7
        }
    }
}