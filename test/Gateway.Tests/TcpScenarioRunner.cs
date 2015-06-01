// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Authentication;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Buffers;

    public class TcpScenarioRunner
    {
        readonly RemoteCertificateValidationCallback remoteCertificateValidationCallback = (sender, certificate, chain, errors) => true;

        Stopwatch stopwatch;
        int scenariosCompletedInPeriod;
        long scenarioTimeSpentInPeriod;

        public async Task RunAsync(TcpScenarioOptions options, TcpScenarioMessage[] messages, CancellationToken cancellationToken)
        {
            //Console.WriteLine("{0} clients execute scenario {1} times [{2}]", options.ClientCount, options.RepeatCount, options.Address);

            this.stopwatch = Stopwatch.StartNew();
            int c = 0;
            var clientTasks = new List<Task>(options.ClientCount);
            while (c < options.ClientCount)
            {
                bool erroneous = false;
                try
                {
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.Connect(options.Address, options.Port);
                    //socket.NoDelay = true;
                    int clientId = c;

#pragma warning disable 4014 //spawning detached client processing
                    clientTasks.Add(Task.Run(() => this.RunClientAsync(socket, options, clientId, messages, cancellationToken), cancellationToken));
#pragma warning restore 4014
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error establishing connection #{0}: {1}: {2}", c, ex.GetType(), ex.Message);
                    erroneous = true;
                }
                if (erroneous)
                {
                    await Task.Delay(1000, cancellationToken);
                }
                else
                {
                    c++;
                }
            }
            //Console.WriteLine("Connected all");

            var completionCts = new CancellationTokenSource();
            CancellationTokenSource terminationCts = CancellationTokenSource.CreateLinkedTokenSource(completionCts.Token, cancellationToken);
            Task reportTask = Task.Run(
                async () =>
                {
                    while (true)
                    {
                        terminationCts.Token.ThrowIfCancellationRequested();
                        await Task.Delay(TimeSpan.FromSeconds(options.ReportInterval), terminationCts.Token);
                        int capturedScenariosInPeriod = Interlocked.Exchange(ref this.scenariosCompletedInPeriod, 0);
                        long capturedTimeSpentInPeriod = Interlocked.Exchange(ref this.scenarioTimeSpentInPeriod, 0L);
                        if (capturedScenariosInPeriod > 0)
                        {
                            Console.WriteLine("throughput: {0:N2} scenarios/sec, avg latency: {1:N2} msec/scenario",
                                (double)capturedScenariosInPeriod / options.ReportInterval, (double)capturedTimeSpentInPeriod / capturedScenariosInPeriod);
                        }
                    }
                },
                terminationCts.Token);

            await Task.WhenAll(clientTasks);

            this.stopwatch.Stop();

            // cleanup
            completionCts.Cancel();

            try
            {
                await reportTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        async Task RunClientAsync(Socket socket, TcpScenarioOptions options, int clientId, TcpScenarioMessage[] scenarioMessages, CancellationToken cancellationToken)
        {
            Stream stream = new NetworkStream(socket);
            if (options.Tls)
            {
                stream = new SslStream(stream, false, this.remoteCertificateValidationCallback);
            }

            using (stream)
            {
                try
                {
                    if (options.Tls)
                    {
                        await ((SslStream)stream).AuthenticateAsClientAsync("", null, SslProtocols.Tls | SslProtocols.Tls11 | SslProtocols.Tls12, false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("TLS error on client#{0}: {1}: {2}", clientId, ex.GetType(), ex.Message);
                }

                try
                {
                    if (options.StartDelay > 0)
                    {
                        await Task.Delay(options.StartDelay, cancellationToken);
                    }

                    for (int i = 0; i < options.RepeatCount; i++)
                    {
                        await this.ExecuteScenarioAsync(scenarioMessages, stream, clientId, cancellationToken);
                    }
                }
                    //catch (Exception ex)
                    //{
                    //    Console.WriteLine("Comm error on client#{0}: {1}: {2}", clientId, ex.GetType(), ex.Message);
                    //}
                finally
                {
                    try
                    {
                        socket.Shutdown(SocketShutdown.Both);
                        socket.Close(options.EndDelay / 1000);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
        }

        async Task ExecuteScenarioAsync(IEnumerable<TcpScenarioMessage> scenarioMessages, Stream stream, int clientId, CancellationToken cancellationToken)
        {
            try
            {
                long startTime = this.stopwatch.ElapsedMilliseconds;

                foreach (IGrouping<int, TcpScenarioMessage> messageGroup in from msg in scenarioMessages group msg by msg.Order)
                {
                    List<TcpScenarioMessage> pendingMessages = messageGroup.ToList();
                    TimeSpan delay = pendingMessages[0].Delay;
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, cancellationToken);
                    }

                    foreach (TcpScenarioMessage message in pendingMessages.Where(x => x.Out).ToArray())
                    {
                        // todo: consider mixing unordered reads & writes
                        await stream.WriteAsync(message.Content, 0, message.Content.Length, cancellationToken);
                        pendingMessages.Remove(message);
                    }

                    while (pendingMessages.Count > 0)
                    {
                        int maxMessageSize = pendingMessages.Max(x => x.Content.Length);
                        IByteBuffer buffer = Unpooled.Buffer(maxMessageSize, maxMessageSize);
                        List<TcpScenarioMessage> draftMessages = pendingMessages.ToList();
                        int bytesToRead = 0;
                        int previouslyVerifiedLength = 0;
                        while (draftMessages.Count > 0)
                        {
                            int pendingLength = draftMessages.Min(x => x.Content.Length);
                            bytesToRead = pendingLength - bytesToRead;
                            do
                            {
                                int read = await stream.ReadAsync(buffer.Array, buffer.WriterIndex + buffer.ArrayOffset, bytesToRead, cancellationToken);
                                // todo: support timeout
                                buffer.SetWriterIndex(buffer.WriterIndex + read);
                            }
                            while (buffer.WriterIndex - buffer.ReaderIndex < bytesToRead);

                            bool matched = false;
                            foreach (TcpScenarioMessage message in draftMessages.Where(x => x.Content.Length == pendingLength).ToArray())
                            {
                                // match complete messages first - best chance of finding a match and skipping forward early
                                if (this.CheckContentMatch(message, previouslyVerifiedLength, buffer))
                                {
                                    matched = true;
                                    pendingMessages.Remove(message);
                                }
                                else
                                {
                                    draftMessages.Remove(message);
                                }
                            }

                            if (matched)
                            {
                                break;
                            }

                            foreach (TcpScenarioMessage message in draftMessages.Where(x => x.Content.Length > pendingLength).ToArray())
                            {
                                if (!this.CheckContentMatch(message, previouslyVerifiedLength, buffer))
                                {
                                    draftMessages.Remove(message);
                                }
                            }

                            if (draftMessages.Count == 0)
                            {
                                throw new InvalidOperationException(string.Format("No message with order {0} could be matched against received byte sequence.", messageGroup.Key));
                            }

                            buffer.SetReaderIndex(buffer.WriterIndex);

                            previouslyVerifiedLength = pendingLength;
                        }
                    }
                }
                Interlocked.Increment(ref this.scenariosCompletedInPeriod);
                Interlocked.Add(ref this.scenarioTimeSpentInPeriod, this.stopwatch.ElapsedMilliseconds - startTime);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error while executing client#{0}: {1}", clientId, ex.Message);
            }
        }

        bool CheckContentMatch(TcpScenarioMessage message, int messageOffset, IByteBuffer actualContent)
        {
            int i = messageOffset;
            actualContent.MarkReaderIndex();
            try
            {
                while (actualContent.IsReadable())
                {
                    int actualByte = actualContent.ReadByte();
                    int primerByte = message.Content[i];

                    int mask;
                    if (message.VerificationMaskMap.TryGetValue(i, out mask))
                    {
                        actualByte &= mask;
                        primerByte &= mask;
                    }
                    if (actualByte != primerByte)
                    {
                        return false;
                    }

                    i++;
                }
                return true;
            }
            finally
            {
                actualContent.ResetReaderIndex();
            }
        }
    }
}