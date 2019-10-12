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

        readonly Settings settings;
        readonly ISessionStatePersistenceProvider sessionStateManager;
        readonly IDeviceIdentityProvider authProvider;
        readonly MessagingBridgeFactoryFunc messagingBridgeFactory;
        readonly CancellationTokenSource lifetimeCancellation;
        readonly IQos2StatePersistenceProvider qos2StateProvider;
        readonly QualityOfService maxSupportedQosToClient;
        IChannelHandlerContext channelContext;
        StateFlags stateFlags;
        IDeviceIdentity identity;
        ISessionState sessionState;
        IMessagingBridge messagingBridge;
        DateTime lastClientActivityTime;
        AckQueue ackQueue;
        RequestAckPairProcessor<AckPendingMessageState, PublishPacket> publishPubAckProcessor;
        RequestAckPairProcessor<AckPendingMessageState, PublishPacket> publishPubRecProcessor;
        RequestAckPairProcessor<CompletionPendingMessageState, PubRelPacket> pubRelPubCompProcessor;
        TimeSpan keepAliveTimeout;
        Queue<Packet> subscriptionChangeQueue; // queue of SUBSCRIBE and UNSUBSCRIBE packets
        Queue<Packet> connectPendingQueue;
        PublishPacket willPacket;
        SemaphoreSlim qos2Semaphore;
        event EventHandler capabilitiesChanged;

        public string ChannelId => this.channelContext.Channel.Id.ToString();

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

        string DeviceId => this.identity.Id;

        int InboundBacklogSize =>
            this.publishPubAckProcessor.BacklogSize
            + this.publishPubRecProcessor.BacklogSize
            + this.pubRelPubCompProcessor.BacklogSize;

        #region IChannelHandler overrides

        public override void ChannelActive(IChannelHandlerContext context)
        {
            this.channelContext = context;

            bool abortOnOutOfOrderAck = this.settings.AbortOnOutOfOrderPubAck;

            this.publishPubAckProcessor = new RequestAckPairProcessor<AckPendingMessageState, PublishPacket>(this.channelContext, this.AcknowledgePublishAsync, abortOnOutOfOrderAck, this);
            this.publishPubAckProcessor.Closed.OnFault(ShutdownOnPubAckFaultAction, this);
            this.publishPubRecProcessor = new RequestAckPairProcessor<AckPendingMessageState, PublishPacket>(this.channelContext, this.AcknowledgePublishReceiveAsync, abortOnOutOfOrderAck, this);
            this.publishPubRecProcessor.Closed.OnFault(ShutdownOnPubRecFaultAction, this);
            this.pubRelPubCompProcessor = new RequestAckPairProcessor<CompletionPendingMessageState, PubRelPacket>(this.channelContext, this.AcknowledgePublishCompleteAsync, abortOnOutOfOrderAck, this);
            this.pubRelPubCompProcessor.Closed.OnFault(ShutdownOnPubCompFaultAction, this);

            this.ackQueue = new AckQueue(pid =>
            {
                var ack = new PubAckPacket();
                ack.PacketId = pid;
                Util.WriteMessageAsync(this.channelContext, ack)
                    .OnFault(ShutdownOnWriteFaultAction, this.channelContext);
            });

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
                this.ProcessMessage(packet);
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

        void ProcessMessage(Packet packet)
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
                    this.Connect((ConnectPacket)packet);
                    break;
                case PacketType.PUBLISH:
                    PerformanceCounters.PublishPacketsReceivedPerSecond.Increment();
                    this.PublishToServerAsync((PublishPacket)packet, null).OnFault(ShutdownOnPublishToServerFaultAction, this);
                    break;
                case PacketType.PUBACK:
                    this.publishPubAckProcessor.Post((PubAckPacket)packet);
                    break;
                case PacketType.PUBREC:
                    this.publishPubRecProcessor.Post((PubRecPacket)packet);
                    break;
                case PacketType.PUBCOMP:
                    this.pubRelPubCompProcessor.Post((PubCompPacket)packet);
                    break;
                case PacketType.SUBSCRIBE:
                case PacketType.UNSUBSCRIBE:
                    this.HandleSubscriptionChange(packet);
                    break;
                case PacketType.PINGREQ:
                    // no further action is needed - keep-alive "timer" was reset by now
                    Util.WriteMessageAsync(this.channelContext, PingRespPacket.Instance)
                        .OnFault(ShutdownOnWriteFaultAction, this.channelContext);
                    break;
                case PacketType.DISCONNECT:
                    CommonEventSource.Log.Verbose("Disconnecting gracefully.", this.identity.ToString(), this.ChannelId);
                    this.Shutdown(this.channelContext, null);
                    break;
                default:
                    ShutdownOnError(this.channelContext, string.Empty, new ProtocolGatewayException(ErrorCode.UnknownPacketType, $"Packet of unsupported type was observed: {packet}, channel id: {this.ChannelId}, identity: {this.identity}"));
                    break;
            }
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

        void HandleSubscriptionChange(Packet packet)
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
                this.ProcessPendingSubscriptionChanges();
            }
        }

        async void ProcessPendingSubscriptionChanges()
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
                        tasks.Add(this.channelContext.WriteAsync(ack));
                    }
                    this.channelContext.Flush();
                    await Task.WhenAll(tasks);
                    PerformanceCounters.PacketsSentPerSecond.IncrementBy(acks.Count);
                }
                while (this.subscriptionChangeQueue.Count > 0);

                this.subscriptionChangeQueue = null;

                this.ResetState(StateFlags.ChangingSubscriptions);
            }
            catch (Exception ex)
            {
                ShutdownOnError(this.channelContext, "-> UN/SUBSCRIBE", ex);
            }
        }

        #endregion

        #region PUBLISH Client -> Server handling

        async Task PublishToServerAsync(PublishPacket packet, string messageType)
        {
            if (!this.IsInState(StateFlags.Connected))
            {
                packet.Release();
                return;
            }

            PreciseTimeSpan startedTimestamp = PreciseTimeSpan.FromStart;

            this.ResumeReadingIfNecessary();

            IMessagingServiceClient sendingClient = this.ResolveSendingClient(packet.TopicName);
            IMessage message = null;
            try
            {
                message = sendingClient.CreateMessage(packet.TopicName, packet.Payload);
                Util.CompleteMessageFromPacket(message, packet, this.settings);

                if (messageType != null)
                {
                    message.Properties[this.settings.ServicePropertyPrefix + MessagePropertyNames.MessageType] = messageType;
                }

                if (!this.IsInState(StateFlags.Closed))
                {
                    switch (packet.QualityOfService)
                    {
                        case QualityOfService.AtMostOnce:
                            // no response necessary
                            await sendingClient.SendAsync(message);
                            PerformanceCounters.MessagesSentPerSecond.Increment();
                            PerformanceCounters.InboundMessageProcessingTime.Register(startedTimestamp);
                            break;
                        case QualityOfService.AtLeastOnce:
                            var task = sendingClient.SendAsync(message);
                            this.ackQueue.Post(packet.PacketId, task);
                            await task;
                            PerformanceCounters.MessagesSentPerSecond.Increment();
                            PerformanceCounters.InboundMessageProcessingTime.Register(startedTimestamp);
                            break;
                        case QualityOfService.ExactlyOnce:
                            ShutdownOnError(this.channelContext, InboundPublishProcessingScope, new ProtocolGatewayException(ErrorCode.ExactlyOnceQosNotSupported, "QoS 2 is not supported."));
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

        void ResumeReadingIfNecessary()
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
                    this.channelContext?.Read();
                }
            }
        }

        #endregion

        #region PUBLISH Server -> Client handling

        void IMessagingChannel.Handle(IMessage message, IMessagingSource callback)
        {
            if (this.channelContext.Executor.InEventLoop)
            {
                this.HandleInternal(message, callback);
            }
            else
            {
                this.channelContext.Executor.Execute((s, m) => this.HandleInternal((IMessage)m, (IMessagingSource)s), callback, message);
            }
        }

        void HandleInternal(IMessage message, IMessagingSource sender)
        {
            try
            {
                Contract.Assert(message != null);

                PerformanceCounters.MessagesReceivedPerSecond.Increment();
                this.PublishToClientAsync(message, sender).OnFault(ShutdownOnPublishFaultAction, this.channelContext);
                message = null;
            }
            catch (MessagingException ex)
            {
                this.ShutdownOnReceiveError(ex);
            }
            catch (Exception ex)
            {
                ShutdownOnError(this.channelContext, ReceiveProcessingScope, ex);
            }
            finally
            {
                message?.Payload.SafeRelease();
            }
        }

        void IMessagingChannel.Close(Exception cause)
        {
            if (this.channelContext.Executor.InEventLoop)
            {
                this.ShutdownOnReceiveError(cause);
            }
            else
            {
                this.channelContext.Executor.Execute(() => this.ShutdownOnReceiveError(cause));
            }
        }

        event EventHandler IMessagingChannel.CapabilitiesChanged
        {
            add { this.capabilitiesChanged += value; }
            remove { this.capabilitiesChanged -= value; }
        }

        async Task PublishToClientAsync(IMessage message, IMessagingSource callback)
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

                    packet = Util.ComposePublishPacket(this.channelContext, message, qos);
                    switch (qos)
                    {
                        case QualityOfService.AtMostOnce:
                            await this.PublishToClientQos0Async(message, callback, packet);
                            break;
                        case QualityOfService.AtLeastOnce:
                            await this.PublishToClientQos1Async(message, callback, packet);
                            break;
                        case QualityOfService.ExactlyOnce:
                            if (this.maxSupportedQosToClient >= QualityOfService.ExactlyOnce)
                            {
                                await this.PublishToClientQos2Async(message, callback, packet);
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
                ShutdownOnError(this.channelContext, "<- PUBLISH", ex);
            }
        }

        static async Task RejectMessageAsync(string messageId, IMessagingSource callback)
        {
            await callback.RejectAsync(messageId); // awaiting guarantees that we won't complete consecutive message before this is completed.
            PerformanceCounters.MessagesRejectedPerSecond.Increment();
        }

        Task PublishToClientQos0Async(IMessage message, IMessagingSource callback, PublishPacket packet)
        {
            if (message.DeliveryCount == 0)
            {
                return Task.WhenAll(
                    callback.CompleteAsync(message.Id),
                    Util.WriteMessageAsync(this.channelContext, packet));
            }
            else
            {
                return callback.CompleteAsync(message.Id);
            }
        }

        Task PublishToClientQos1Async(IMessage message, IMessagingSource callback, PublishPacket packet)
        {
            return this.publishPubAckProcessor.SendRequestAsync(packet,
                new AckPendingMessageState(message, callback, packet));
        }

        async Task PublishToClientQos2Async(IMessage message, IMessagingSource callback, PublishPacket packet)
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
                    deliveryTask = this.publishPubRecProcessor.SendRequestAsync(packet,
                        new AckPendingMessageState(message, callback, packet));
                }
                else
                {
                    deliveryTask = this.PublishReleaseToClientAsync(packetId, new MessageFeedbackChannel(message.Id, callback), messageInfo, PreciseTimeSpan.FromStart);
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

        Task PublishReleaseToClientAsync(int packetId, MessageFeedbackChannel feedbackChannel, IQos2MessageDeliveryState messageState, PreciseTimeSpan startTimestamp)
        {
            var pubRelPacket = new PubRelPacket();
            pubRelPacket.PacketId = packetId;
            return this.pubRelPubCompProcessor.SendRequestAsync(pubRelPacket,
                new CompletionPendingMessageState(packetId, messageState, startTimestamp, feedbackChannel));
        }

        async Task AcknowledgePublishAsync(AckPendingMessageState message)
        {
            this.ResumeReadingIfNecessary();

            // todo: is try-catch needed here?
            try
            {
                await message.FeedbackChannel.CompleteAsync();

                PerformanceCounters.OutboundMessageProcessingTime.Register(message.StartTimestamp);
            }
            catch (Exception ex)
            {
                ShutdownOnError(this.channelContext, "-> PUBACK", ex);
            }
        }

        async Task AcknowledgePublishReceiveAsync(AckPendingMessageState message)
        {
            this.ResumeReadingIfNecessary();

            // todo: is try-catch needed here?
            try
            {
                IQos2MessageDeliveryState messageInfo = this.qos2StateProvider.Create(message.SequenceNumber);
                await this.qos2StateProvider.SetMessageAsync(this.identity, message.PacketId, messageInfo);

                await this.PublishReleaseToClientAsync(message.PacketId, message.FeedbackChannel, messageInfo, message.StartTimestamp);
            }
            catch (Exception ex)
            {
                ShutdownOnError(this.channelContext, "-> PUBREC", ex);
            }
        }

        async Task AcknowledgePublishCompleteAsync(CompletionPendingMessageState message)
        {
            this.ResumeReadingIfNecessary();

            try
            {
                await message.FeedbackChannel.CompleteAsync();

                await this.qos2StateProvider.DeleteMessageAsync(this.identity, message.PacketId, message.DeliveryState);

                PerformanceCounters.OutboundMessageProcessingTime.Register(message.StartTimestamp);
            }
            catch (Exception ex)
            {
                ShutdownOnError(this.channelContext, "-> PUBCOMP", ex);
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

        void ShutdownOnReceiveError(Exception cause) => ShutdownOnError(this.channelContext, ReceiveProcessingScope, cause);

        #endregion

        #region CONNECT handling and lifecycle management

        /// <summary>
        ///     Performs complete initialization of <see cref="MqttAdapter" /> based on received CONNECT packet.
        /// </summary>
        /// <param name="context"><see cref="IChannelHandlerContext" /> instance.</param>
        /// <param name="packet">CONNECT packet.</param>
        async void Connect(ConnectPacket packet)
        {
            bool connAckSent = false;

            Exception exception = null;
            try
            {
                if (!this.IsInState(StateFlags.WaitingForConnect))
                {
                    ShutdownOnError(this.channelContext, ConnectProcessingScope, new ProtocolGatewayException(ErrorCode.DuplicateConnectReceived, "CONNECT has been received in current session already. Only one CONNECT is expected per session."));
                    return;
                }

                this.stateFlags = StateFlags.ProcessingConnect;
                this.identity = await this.authProvider.GetAsync(packet.ClientId,
                    packet.Username, packet.Password, this.channelContext.Channel.RemoteAddress);

                if (!this.identity.IsAuthenticated)
                {
                    CommonEventSource.Log.Info("ClientNotAuthenticated", this.ChannelId, $"Client ID: {packet.ClientId}; Username: {packet.Username}");
                    connAckSent = true;
                    await Util.WriteMessageAsync(this.channelContext, new ConnAckPacket
                    {
                        ReturnCode = ConnectReturnCode.RefusedNotAuthorized
                    });
                    PerformanceCounters.ConnectionFailedAuthPerSecond.Increment();
                    ShutdownOnError(this.channelContext, ConnectProcessingScope, new ProtocolGatewayException(ErrorCode.AuthenticationFailed, "Authentication failed."));
                    return;
                }

                CommonEventSource.Log.Info("ClientAuthenticated", this.ChannelId, this.Id);

                this.messagingBridge = await this.messagingBridgeFactory(this.identity, this.lifetimeCancellation.Token);

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
                await Util.WriteMessageAsync(this.channelContext, new ConnAckPacket
                {
                    SessionPresent = sessionPresent,
                    ReturnCode = ConnectReturnCode.Accepted
                });

                this.CompleteConnect();
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
                        await Util.WriteMessageAsync(this.channelContext, new ConnAckPacket
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

                ShutdownOnError(this.channelContext, ConnectProcessingScope, exception);
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
        void CompleteConnect()
        {
            CommonEventSource.Log.Info("Connection established.", this.ChannelId, this.Id);

            if (this.keepAliveTimeout > TimeSpan.Zero)
            {
                CheckKeepAlive(this.channelContext);
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
                    this.ProcessMessage(packet);
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
            try
            {
                this.publishPubAckProcessor.Close();
                this.publishPubRecProcessor.Close();
                this.pubRelPubCompProcessor.Close();

                await Task.WhenAll(
                    this.CompletePublishAsync(will),
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
                if (this.messagingBridge != null)
                {
                    await this.messagingBridge.DisposeAsync(cause);
                }
            }
            catch (Exception ex)
            {
                CommonEventSource.Log.Info("Failed to close IoT Hub Client cleanly: " + ex.ToString(), this.ChannelId, this.Id);
            }
        }

        async Task CompletePublishAsync(PublishPacket will)
        {
            var completionTasks = new List<Task>();

            if (will != null)
            {
                try
                {
                    await this.PublishToServerAsync(will, MessageTypes.Will);
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
            if (this.InboundBacklogSize >= this.settings.MaxPendingInboundAcknowledgements || this.ackQueue.Count >= this.settings.MaxPendingInboundMessages)
            {
                return false;
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