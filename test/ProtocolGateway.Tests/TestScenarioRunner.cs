// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;
    using Microsoft.Azure.Devices.ProtocolGateway.Extensions;

    public class TestScenarioRunner : ChannelHandlerAdapter
    {
        IEnumerator<TestScenarioStep> testScenario;
        readonly Func<Func<object>, IEnumerable<TestScenarioStep>> testScenarioProvider;
        readonly TaskCompletionSource completion;
        readonly TimeSpan sendTimeout;
        readonly TimeSpan responseTimeout;
        object lastReceivedMessage;
        CancellationTokenSource responseTimeoutCts;

        public TestScenarioRunner(Func<Func<object>, IEnumerable<TestScenarioStep>> testScenarioProvider, TaskCompletionSource completion, TimeSpan sendTimeout, TimeSpan responseTimeout)
        {
            Contract.Requires(completion != null);
            this.testScenarioProvider = testScenarioProvider;
            this.completion = completion;
            this.sendTimeout = sendTimeout;
            this.responseTimeout = responseTimeout;
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            this.testScenario = this.testScenarioProvider(() => this.lastReceivedMessage).GetEnumerator();
            this.ContinueScenarioExecution(context);
        }

        public override void ChannelRead(IChannelHandlerContext context, object message)
        {
            CancellationTokenSource cts = this.responseTimeoutCts;
            if (cts != null)
            {
                cts.Cancel();
            }
            this.lastReceivedMessage = message;
            this.ContinueScenarioExecution(context);
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            //context.Flush();
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            this.completion.TrySetException(exception);
            context.CloseAsync();
        }

        void ContinueScenarioExecution(IChannelHandlerContext context)
        {
            if (!this.testScenario.MoveNext())
            {
                context.CloseAsync()
                    .ContinueWith(
                        t => this.completion.TrySetException(t.Exception),
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                this.completion.TryComplete();
                return;
            }

            TestScenarioStep currentStep = this.testScenario.Current;

            if (currentStep.Delay > TimeSpan.Zero)
            {
                context.Channel.EventLoop.ScheduleAsync((ctx, state) => this.ExecuteStep((IChannelHandlerContext)ctx, (TestScenarioStep)state), context, currentStep, currentStep.Delay);
            }
            else
            {
                this.ExecuteStep(context, currentStep);
            }
        }

        void ExecuteStep(IChannelHandlerContext context, TestScenarioStep currentStep)
        {
            if (!context.Channel.Open)
            {
                // todo: dispose scheduled work in case of channel closure instead?
                return;
            }

            Task lastTask = null;
            object lastMessage = null;
            foreach (object message in currentStep.Messages)
            {
                lastMessage = message;
                var writeTimeoutCts = new CancellationTokenSource();
                Task task = context.WriteAsync(message);
                object timeoutExcMessage = message;
                context.Channel.EventLoop.ScheduleAsync(
                    () => this.completion.TrySetException(new TimeoutException(string.Format("Sending of message did not complete in time: {0}", timeoutExcMessage))),
                    this.sendTimeout,
                    writeTimeoutCts.Token);
                task.ContinueWith(
                    t => writeTimeoutCts.Cancel(),
                    TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);
                task.OnFault(t => this.completion.TrySetException(t.Exception));

                lastTask = task;
            }

            if (currentStep.WaitForFeedback)
            {
                if (this.responseTimeout > TimeSpan.Zero)
                {
                    this.responseTimeoutCts = new CancellationTokenSource();
                    if (lastTask == null)
                    {
                        this.ScheduleReadTimeoutCheck(context, null);
                    }
                    else
                    {
                        lastTask.ContinueWith(
                            t => this.ScheduleReadTimeoutCheck(context, lastMessage),
                            TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);
                    }
                }
            }

            context.Flush();

            if (!currentStep.WaitForFeedback)
            {
                context.Channel.EventLoop.Execute(
                    ctx => this.ContinueScenarioExecution((IChannelHandlerContext)ctx),
                    context);
            }
        }

        void ScheduleReadTimeoutCheck(IChannelHandlerContext context, object lastMessage)
        {
            context.Channel.EventLoop.ScheduleAsync(
                () => this.completion.TrySetException(new TimeoutException(string.Format("Timed out waiting for response to {0} message.", lastMessage))),
                this.responseTimeout,
                this.responseTimeoutCts.Token);
        }
    }
}