// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace MqttClient
{
    using System;
    using System.Linq;
    using System.Net.Security;
    using System.Text;
    using CommandLine;
    using CommandLine.Text;
    using uPLibrary.Networking.M2Mqtt;

    class Program
    {
        static readonly RemoteCertificateValidationCallback RemoteCertificateValidationCallback = (sender, certificate, chain, errors) => true;

        static void Main(string[] args)
        {
            var options = new Options();
            if (!Parser.Default.ParseArguments(args, options))
            {
                Console.WriteLine(HelpText.AutoBuild(options).ToString());
                return;
            }

            // todo: impl mqtt client in DotNetty and use that instead
            var client = new MqttClient(options.Address, options.Port, options.Tls, RemoteCertificateValidationCallback, null);
            client.MqttMsgSubscribed += (sender, e) => Console.WriteLine("SUBACK received: {0} {1}", e.MessageId, e.GrantedQoSLevels);
            client.ConnectionClosed += (sender, e) => Console.WriteLine("Connection closed");
            client.MqttMsgUnsubscribed += (sender, e) => Console.WriteLine("UNSUBACK received: {0}", e.MessageId);
            client.MqttMsgPublishReceived += (sender, e) => Console.WriteLine("PUBLISH received: {0} {1} {2}\nTopic name: {3}\nMessage:\n{4}",
                e.DupFlag, e.QosLevel, e.Retain, e.Topic, MessageBodyToString(e.Message));
            client.MqttMsgPublished += (sender, e) => Console.WriteLine("PUBLISH completed: " + e.MessageId + " " + e.IsPublished);
            while (true)
            {
                string commandData = Console.ReadLine();
                try
                {
                    string[] arguments = commandData.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    switch (arguments[0].ToUpperInvariant())
                    {
                        case "CONNECT":
                            bool cleanSession;
                            ushort keepAlive;
                            client.Connect(
                                arguments[1],
                                arguments.Length > 4 ? arguments[4] : null,
                                arguments.Length > 5 ? arguments[5] : null,
                                arguments.Length > 2 ? bool.TryParse(arguments[2], out cleanSession) && cleanSession : false,
                                (ushort)(arguments.Length > 3 ? ushort.TryParse(arguments[3], out keepAlive) ? keepAlive : 60 : 60));
                            break;
                        case "SUBSCRIBE":
                            string[] topics = arguments.Skip(1).ToArray();
                            client.Subscribe(topics, Enumerable.Repeat((byte)1, topics.Length).ToArray());
                            break;
                        case "DISCONNECT":
                            client.Disconnect();
                            break;
                        case "UNSUBSCRIBE":
                            client.Unsubscribe(arguments.Skip(1).ToArray());
                            break;
                        case "PUBLISH":
                            client.Publish(arguments[2], Encoding.UTF8.GetBytes(string.Join(" ", arguments.Skip(3))), byte.Parse(arguments[1]), false);
                            break;
                        default:
                            throw new InvalidOperationException(string.Format("Unknown command `{0}`", arguments[0]));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(@"Example commands:
CONNECT <clientId> [<cleanSession>] [<keep-alive timeout>] [<username>] [<password>]
SUBSCRIBE <topic-name1> [<topic-name2>] ...
UNSUBSCRIBE <topic-name1> [topic-name2] ...
PUBLISH <qos> <topic-name> <message>
DISCONNECT");
                }
            }
        }

        static string MessageBodyToString(byte[] message)
        {
            return Encoding.UTF8.GetString(message);
        }
    }
}