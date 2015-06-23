// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Core.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Transport.Channels;

    public sealed class BridgeDriver : ChannelHandlerAdapter
    {
        static readonly Action<object> CheckConnectTimeoutCallback = CheckConnectionTimeout;
        static readonly Action<object> CheckKeepAliveCallback = CheckKeepAlive;
        static readonly Action<object> CheckRetransmissionNeededCallback = CheckRetransmissionNeeded;
        const QualityOfService MaxSupportedQoS = QualityOfService.AtLeastOnce;

        readonly Settings settings;
        StateFlags stateFlags;
        DateTime lastClientActivityTime;
        ISessionState sessionState;
        DeviceClient iotHubClient;
        Queue<PublishPacket> publishQueue; // queue of inbound PUBLISH packets pending processing
        Queue<AckPendingState> ackPendingQueue;
        TimeSpan keepAliveTimeout;
        int lastIssuedPacketId;
        Queue<Packet> subscriptionChangeQueue; // queue of SUBSCRIBE and UNSUBSCRIBE packets
        readonly ISessionStateManager sessionStateManager;
        string clientId;
        readonly IAuthenticationProvider authProvider;
        Queue<Packet> connectPendingQueue;
        IByteBuffer willMessage;
        QualityOfService willQoS;

        public BridgeDriver(Settings settings, ISessionStateManager sessionStateManager, IAuthenticationProvider authProvider)
        {
            Contract.Requires(settings != null);
            Contract.Requires(sessionStateManager != null);
            Contract.Requires(authProvider != null);

            this.settings = settings;
            this.sessionStateManager = sessionStateManager;
            this.authProvider = authProvider;
        }

        Queue<PublishPacket> PublishQueue
        {
            get { return this.publishQueue ?? (this.publishQueue = new Queue<PublishPacket>(4)); }
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

        public override void ChannelActive(IChannelHandlerContext context)
        {
            this.stateFlags = StateFlags.WaitingForConnect;
            TimeSpan? timeout = this.settings.ConnectArrivalTimeout;
            if (timeout.HasValue)
            {
                context.Channel.Executor.Schedule(CheckConnectTimeoutCallback, context, timeout.Value);
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
            context.Read(); // todo: control read rate through setting
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            this.CloseAsync(context, false);

            base.ChannelInactive(context);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            CloseOnError(context, "Exception encountered: " + exception);

            base.ExceptionCaught(context, exception);
        }

        void ProcessMessage(IChannelHandlerContext context, Packet packet)
        {
            switch (packet.PacketType)
            {
                case PacketType.CONNECT:
                    this.OnConnectAsync(context, (ConnectPacket)packet);
                    break;
                case PacketType.PUBLISH:
                    this.OnPublish(context, (PublishPacket)packet);
                    break;
                case PacketType.PUBACK:
                    this.OnPubAckAsync(context, (PubAckPacket)packet);
                    break;
                case PacketType.SUBSCRIBE:
                case PacketType.UNSUBSCRIBE:
                    this.OnSubscriptionChange(context, packet);
                    break;
                case PacketType.PINGREQ:
                    context.WriteAsync(PingRespPacket.Instance); // no further action is needed - keep-alive "timer" was reset by now
                    break;
                case PacketType.DISCONNECT:
                    BridgeEventSource.Log.Verbose("Disconnecting gracefully.", this.clientId);
                    this.CloseAsync(context, true);
                    break;
                default:
                    CloseOnError(context, "Packet of unsupported type was observed: " + packet);
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
                                acks.Add(ApplySubscription(newState, (SubscribePacket)packet));
                                break;
                            case PacketType.UNSUBSCRIBE:
                                acks.Add(this.ApplyUnsubscribe(newState, (UnsubscribePacket)packet));
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                    queue.Clear();

                    if (!this.sessionState.IsTransient)
                    {
                        // save updated session state, make it current once successfully set
                        await this.sessionStateManager.SetAsync(this.clientId, newState);
                    }

                    this.sessionState = newState;

                    // release ACKs

                    foreach (Packet ack in acks)
                    {
                        await context.WriteAsync(ack); // todo: batch send - through FlushAsync
                    }
                }
                while (this.subscriptionChangeQueue.Count > 0);

                this.stateFlags &= ~StateFlags.ChangingSubscriptions;
            }
            catch (Exception ex)
            {
                CloseOnError(context, "-> UN/SUBSCRIBE", ex);
            }
        }

        static SubAckPacket ApplySubscription(ISessionState session, SubscribePacket packet)
        {
            List<SubscriptionRequest> subscriptions = session.Subscriptions;
            var returnCodes = new List<QualityOfService>(subscriptions.Count);
            foreach (SubscriptionRequest request in packet.Requests)
            {
                for (int i = subscriptions.Count - 1; i >= 0; i--)
                {
                    if (subscriptions[i].TopicFilter.Equals(request.TopicFilter, StringComparison.Ordinal))
                    {
                        subscriptions.RemoveAt(i);
                        break;
                    }
                }

                var finalQoS = (QualityOfService)Math.Min((int)request.RequestedQualityOfService, (int)MaxSupportedQoS);
                subscriptions.Add(finalQoS == request.RequestedQualityOfService ? request : new SubscriptionRequest(request.TopicFilter, finalQoS));
                returnCodes.Add(finalQoS);
            }
            var ack = new SubAckPacket
            {
                PacketId = packet.PacketId,
                ReturnCodes = returnCodes
            };
            return ack;
        }

        public UnsubAckPacket ApplyUnsubscribe(ISessionState session, UnsubscribePacket packet)
        {
            List<SubscriptionRequest> subscriptions = session.Subscriptions;
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

        void OnPublish(IChannelHandlerContext context, PublishPacket packet)
        {
            this.PublishQueue.Enqueue(packet);

            if (!this.IsInState(StateFlags.Publishing))
            {
                // start publishing if not started yet
                this.stateFlags |= StateFlags.Publishing;
                this.PublishFromQueueAsync(context);
            }
        }

        async void PublishFromQueueAsync(IChannelHandlerContext context)
        {
            try
            {
                do
                {
                    PublishPacket packet = this.PublishQueue.Dequeue();

                    Message message = Util.ConvertPacketToMessage(packet, this.settings);

                    await this.iotHubClient.SendEventAsync(message);

                    switch (packet.QualityOfService)
                    {
                        case QualityOfService.AtMostOnce:
                            // no response necessary
                            break;
                        case QualityOfService.AtLeastOnce:
                            await context.WriteAsync(new PubAckPacket // todo: await out of sequence?
                            {
                                PacketId = packet.PacketId
                            });
                            break;
                        case QualityOfService.ExactlyOnce:
                            CloseOnError(context, "QoS 2 is not supported.");
                            break;
                        default:
                            throw new InvalidOperationException("Unexpected QoS level: " + packet.QualityOfService);
                    }
                }
                while (this.PublishQueue.Count > 0);

                this.stateFlags &= ~StateFlags.Publishing;
            }
            catch (Exception ex)
            {
                CloseOnError(context, "-> PUBLISH", ex);
            }
        }

        #endregion

        #region PUBLISH Server->Client

        async void ReceiveOutboundAsync(IChannelHandlerContext context)
        {
            Debug.Assert(!this.IsInState(StateFlags.Receiving));
            this.stateFlags |= StateFlags.Receiving;

            try
            {
                while (!this.IsInAnyState(StateFlags.Retransmitting | StateFlags.Closed) && (this.ackPendingQueue == null || this.ackPendingQueue.Count < this.settings.MaxOutstandingOutboundMessages))
                {
                    Message message = await this.iotHubClient.ReceiveAsync(TimeSpan.MaxValue); // todo: closure from other code execution can/will cause this to fail -- must be controlled failure (warning log)
                    if (message == null)
                    {
                        // link to IoT Hub has been closed
                        return;
                    }

                    // todo: check delivery count
                    // todo: disable device if same message is not ACKed X times (based on message.DeliveryCount in IoT Hub)?
                    // todo: or deadletter the message?

                    string topicName = Util.DeriveTopicName(message, this.settings);
                    QualityOfService maxRequestedQoS;
                    if (!this.TryMatchSubscription(topicName, out maxRequestedQoS))
                    {
                        // no matching subscription found - complete the message without publishing
                        await this.iotHubClient.CompleteAsync(message.LockToken); // todo: offload await?
                        continue;
                    }

                    // todo: how can we detect/handle poison message?

                    QualityOfService qos = Util.DeriveQoS(message, this.settings);
                    if (maxRequestedQoS < qos)
                    {
                        qos = maxRequestedQoS;
                    }

                    PublishPacket packet = Util.ComposePublishPacket(context, qos, message, topicName);
                    switch (qos)
                    {
                        case QualityOfService.AtMostOnce:
                            // todo: if message.DeliveryCount indicates retransmission, should we drop the message?
                            this.PublishToClientQoS0Async(context, message, packet);
                            break;
                        case QualityOfService.AtLeastOnce:
                            this.PublishToClientQoS1Async(context, message, packet);
                            break;
                        default:
                            throw new InvalidOperationException("QoS cannot be provided.");
                    }
                }
            }
            catch (Exception ex)
            {
                CloseOnError(context, "Receive", ex);
            }

            this.stateFlags &= ~StateFlags.Receiving;
        }

        async void OnPubAckAsync(IChannelHandlerContext context, PubAckPacket packet)
        {
            Queue<AckPendingState> queue = this.ackPendingQueue;
            if (queue == null || queue.Count == 0)
            {
                // todo: consider more tolerable out-of-order PUBACK handling (due to retransmission)
                CloseOnError(context, string.Format("Unexpected acknowledgement with packet id of {0} while no message was pending acknowledgement.", packet.PacketId));
                return;
            }

            AckPendingState firstPending = queue.Dequeue();
            if (packet.PacketId != firstPending.PacketId)
            {
                // todo: consider more tolerable out-of-order PUBACK handling (due to retransmission)
                CloseOnError(context, string.Format("out of order PUBACK. Expected: {0}. Actual: {1}.", firstPending.PacketId, packet.PacketId));
                return;
            }

            try
            {
                await this.iotHubClient.CompleteAsync(firstPending.LockToken);

                if (this.IsInState(StateFlags.Retransmitting))
                {
                    if (queue.Count > 0)
                    {
                        this.RetransmitOutboundFromQueueAsync(context);
                        return;
                    }
                    else
                    {
                        this.stateFlags &= ~StateFlags.Retransmitting;
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
                CloseOnError(context, "-> PUBACK", ex);
            }
        }

        async void PublishToClientQoS0Async(IChannelHandlerContext context, Message message, PublishPacket packet)
        {
            try
            {
                await this.iotHubClient.CompleteAsync(message.LockToken); // complete message with IoT Hub first to make sure we don't deliver it more than once
                await context.WriteAsync(packet);
            }
            catch (Exception ex)
            {
                CloseOnError(context, "<- PUBLISH (QoS 0)", ex);
            }
        }

        async void PublishToClientQoS1Async(IChannelHandlerContext context, Message message, PublishPacket packet)
        {
            try
            {
                int packetId = this.GetNextPacketId();
                packet.PacketId = packetId;
                this.AckPendingQueue.Enqueue(new AckPendingState(message.MessageId, packetId, packet.QualityOfService, message.LockToken));
                if (this.IsInState(StateFlags.Retransmitting))
                {
                    // retransmission is underway so the message has to be sent after retransmission is done;
                    // we cannot abandon message right now as it would mess up the order so we leave message in the queue without sending it.
                    return;
                }

                if (!this.IsInState(StateFlags.RetransmissionCheckScheduled) && this.settings.DeviceReceiveAckCanTimeout)
                {
                    // if retransmission is configured, schedule check for timeout if posting first message in queue
                    this.ScheduleOutboundRetransmissionCheck(context, this.settings.DeviceReceiveAckTimeout);
                }

                try
                {
                    await context.WriteAsync(packet);
                }
                catch (Exception ex)
                {
                    // todo: specialize exception?
                    CloseOnError(context, "Exception while sending message: " + ex);
                }
            }
            catch (Exception ex)
            {
                CloseOnError(context, "<- PUBLISH (QoS 1)", ex);
            }
        }

        void ScheduleOutboundRetransmissionCheck(IChannelHandlerContext context, TimeSpan delay)
        {
            if (this.IsInState(StateFlags.RetransmissionCheckScheduled))
            {
                this.stateFlags |= StateFlags.RetransmissionCheckScheduled;
                context.Channel.Executor.Schedule(CheckRetransmissionNeededCallback, context, delay);
            }
        }

        static void CheckRetransmissionNeeded(object ctx)
        {
            var context = (IChannelHandlerContext)ctx;
            var self = (BridgeDriver)context.Handler;
            self.stateFlags &= ~StateFlags.RetransmissionCheckScheduled; // unset "retransmission check scheduled" flag
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
                    return;
                }

                if (message.MessageId != messageInfo.MessageId)
                {
                    // it is an indication that queue is being accessed not exclusively - terminate the connection
                    CloseOnError(context, string.Format("Expected to receive message with id of {0} but saw a message with id of {1}. Protocol Gateway only supports exclusive connection to IoT Hub.",
                        messageInfo.MessageId, message.MessageId));
                    return;
                }

                // todo: check delivery count
                // todo: disable device if same message is not ACKed X times (based on message.DeliveryCount in IoT Hub)?
                // todo: or deadletter the message?

                PublishPacket packet = Util.ComposePublishPacket(context, messageInfo.QualityOfService, message, Util.DeriveTopicName(message, this.settings));
                messageInfo.Reset(message.LockToken);
                await context.WriteAsync(packet);

                if (this.settings.DeviceReceiveAckCanTimeout)
                {
                    this.ScheduleOutboundRetransmissionCheck(context, this.settings.DeviceReceiveAckTimeout);
                }
            }
            catch (Exception ex)
            {
                CloseOnError(context, "<- PUBLISH (QoS 1) retry", ex);
            }
        }

        bool TryMatchSubscription(string topicName, out QualityOfService qos)
        {
            bool found = false;
            qos = QualityOfService.AtMostOnce;
            foreach (SubscriptionRequest subscription in this.sessionState.Subscriptions)
            {
                if ((!found || subscription.RequestedQualityOfService > qos) && Util.CheckTopicFilterMatch(topicName, subscription.TopicFilter))
                {
                    found = true;
                    qos = subscription.RequestedQualityOfService;
                    if (qos >= MaxSupportedQoS)
                    {
                        qos = MaxSupportedQoS;
                        break;
                    }
                }
            }
            return found;
        }

        int GetNextPacketId()
        {
            bool inUse;
            int newPacketId = this.lastIssuedPacketId;
            do
            {
                newPacketId++;
                if (newPacketId > ushort.MaxValue)
                {
                    newPacketId = 1;
                }

                // check if new packet id is not used by any of outstanding packets
                inUse = false;
                foreach (AckPendingState pending in this.AckPendingQueue)
                {
                    if (pending.PacketId == newPacketId)
                    {
                        inUse = true;
                        break;
                    }
                }
            }
            while (inUse);
            this.lastIssuedPacketId = newPacketId;
            return newPacketId;
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
                    CloseOnError(context, "CONNECT has been received in current session already. Only one CONNECT is expected per session.");
                    return;
                }

                this.stateFlags = StateFlags.ProcessingConnect;
                AuthenticationResult authResult = await this.authProvider.AuthenticateAsync(packet.ClientId, packet.Username, packet.Password);
                if (!authResult.IsSuccessful)
                {
                    connAckSent = true;
                    await context.WriteAsync(new ConnAckPacket
                    {
                        ReturnCode = ConnectReturnCode.RefusedNotAuthorized
                    });
                    CloseOnError(context, "Authentication failed.");
                    return;
                }

                this.clientId = authResult.DeviceId;

                bool sessionPresent = await this.EstablishSessionStateAsync(this.clientId, packet.CleanSession);

                string connectionString = this.ComposeIoTHubConnectionString(authResult.Scope, authResult.Token);
                // todo: wait for connection to IoT Hub and ACK with SERVER_UNAVAILABLE if unable to connect
                this.iotHubClient = DeviceClient.CreateFromConnectionString(connectionString, this.clientId);
                if (this.sessionState.IsTransient)
                {
                    // todo: deplete the device queue in iot hub
                    // await this.iotHubClient.ClearAsync();
                }

                this.keepAliveTimeout = this.DeriveKeepAliveTimeout(packet);
                if (packet.HasWill)
                {
                    this.willMessage = packet.WillMessage;
                    this.willQoS = packet.WillQualityOfService;
                }

                // todo: here or after queued messages are processed as well? does it make sense to wait until after first SUBSCRIBE packet if there was no active subscriptions in session state already?
                this.ReceiveOutboundAsync(context);

                connAckSent = true;
                await context.WriteAsync(new ConnAckPacket
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
                        await context.WriteAsync(new ConnAckPacket
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

                CloseOnError(context, "CONNECT", exception);
            }
        }

        async Task<bool> EstablishSessionStateAsync(string deviceId, bool cleanSession)
        {
            if (cleanSession)
            {
                ISessionState existingSessionState = await this.sessionStateManager.GetAsync(deviceId);
                if (existingSessionState != null)
                {
                    await this.sessionStateManager.DeleteAsync(deviceId, existingSessionState);
                    // todo: loop in case of concurrent access? how will we resolve conflict with concurrent connections?
                }

                this.sessionState = this.sessionStateManager.Create(true);
                return false;
            }
            else
            {
                this.sessionState = await this.sessionStateManager.GetAsync(deviceId);
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
            var timeout = TimeSpan.FromSeconds(packet.KeepAlive * 1.5);
            TimeSpan? maxTimeout = this.settings.MaxKeepAliveTimeout;
            if (maxTimeout.HasValue && this.keepAliveTimeout > maxTimeout.Value)
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
            BridgeEventSource.Log.Verbose("Connection established.", this.clientId);
            
            CheckKeepAlive(context);

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
                CloseOnError(context, "Connection timed out on waiting for CONNECT packet from client.");
            }
        }

        static void CheckKeepAlive(object ctx)
        {
            var context = (IChannelHandlerContext)ctx;
            var self = (BridgeDriver)context.Handler;
            TimeSpan elapsedSinceLastActive = DateTime.UtcNow - self.lastClientActivityTime;
            if (elapsedSinceLastActive > self.keepAliveTimeout)
            {
                CloseOnError(context, "Keep Alive timed out.");
                return;
            }

            context.Channel.Executor.Schedule(CheckKeepAliveCallback, context, self.keepAliveTimeout - elapsedSinceLastActive); // todo: allocation (QueueNode): inherit "callback" from recyclable queue node
        }

        static void CloseOnError(IChannelHandlerContext context, string origin, Exception exception)
        {
            CloseOnError(context, string.Format("Exception occured ({0}): {1}", origin, exception));
        }

        static void CloseOnError(IChannelHandlerContext context, string reason)
        {
            Contract.Requires(!string.IsNullOrEmpty(reason));

            var self = (BridgeDriver)context.Handler;
            if (!self.IsInState(StateFlags.Closed))
            {
                BridgeEventSource.Log.Warning(string.Format("Closing connection ({0}, {1}): {2}", context.Channel.RemoteEndpoint, self.clientId, reason));
                self.CloseAsync(context, false);
            }

        }

        async void CloseAsync(IChannelHandlerContext context, bool graceful)
        {
            if (!this.IsInState(StateFlags.Closed))
            {
                this.stateFlags |= StateFlags.Closed; // "or" not to interfere with ongoing logic which has to honor Closed state when it's right time to do (case by case)
                IByteBuffer willPayload = !graceful && this.IsInState(StateFlags.Connected) ? this.willMessage : null;
                await Task.WhenAll(context.CloseAsync(), this.CloseIotHubConnectionAsync(willPayload));
            }
        }

        async Task CloseIotHubConnectionAsync(IByteBuffer willPayload)
        {
            if (willPayload != null)
            {
                try
                {
                    // todo: this matches QoS 0 guarantees. Support QoS 1 as well.
                    var message = new Message(willPayload.ToArray());
                    message.Properties[this.settings.TopicNameProperty] = this.settings.WillTopicName;
                    await this.iotHubClient.SendEventAsync(message);
                }
                catch (Exception ex)
                {
                    BridgeEventSource.Log.Warning("Failed sending Will Message.", ex);
                }
            }

            try
            {
                await this.iotHubClient.CloseAsync();
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
                CloseOnError(context, "First packet in the session must be CONNECT. Observed: " + packet);
                return true;
            }

            this.ConnectPendingQueue.Enqueue(packet);
            return true;
        }

        string ComposeIoTHubConnectionString(AuthenticationScope authScope, string authToken)
        {
            var csb = new IotHubConnectionStringBuilder(this.settings.IotHubConnectionString);
            switch (authScope)
            {
            case AuthenticationScope.None:
                // use connection string provided credentials
                break;
            case AuthenticationScope.Device:
                csb.CredentialScope = CredentialScope.Device;
                csb.SharedAccessSignature = authToken;
                break;
            case AuthenticationScope.Hub:
                csb.CredentialScope = CredentialScope.IotHub;
                csb.SharedAccessSignature = authToken;
                break;
            default:
                throw new InvalidOperationException("Unexpected AuthenticationScope value: " + authScope);
            }

            return csb.ToString();
        }

        #endregion

        bool IsInState(StateFlags stateFlagsToCheck)
        {
            return (this.stateFlags & stateFlagsToCheck) == stateFlagsToCheck;
        }

        bool IsInAnyState(StateFlags stateFlagsToCheck)
        {
            return (this.stateFlags & stateFlagsToCheck) != 0;
        }

        [Flags]
        enum StateFlags
        {
            WaitingForConnect = 1,
            ProcessingConnect = 2,
            Connected = 4,
            Publishing = 8,
            ChangingSubscriptions = 16,
            Receiving = 32,
            RetransmissionCheckScheduled = 64,
            Retransmitting = 128,
            Closed
        }

        class AckPendingState // todo: recycle
        {
            public AckPendingState(string messageId, int packetId, QualityOfService qos, string lockToken)
            {
                this.MessageId = messageId;
                this.PacketId = packetId;
                this.QualityOfService = qos;
                this.LockToken = lockToken;
                this.PublishTime = DateTime.UtcNow;
            }

            public string MessageId { get; private set; }

            public int PacketId { get; private set; }

            public string LockToken { get; private set; }

            public DateTime PublishTime { get; private set; }

            public QualityOfService QualityOfService { get; private set; }

            public void Reset(string lockToken)
            {
                this.LockToken = lockToken;
                this.PublishTime = DateTime.UtcNow;
            }
        }
    }
}