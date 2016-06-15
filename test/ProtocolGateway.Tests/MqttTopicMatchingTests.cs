// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Tests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Devices.ProtocolGateway.Mqtt;
    using Xunit;

    public class MqttTopicMatchingTests
    {
        [Theory]
        [InlineData("abc/def", "abc/def", true)]
        [InlineData("abc/def", "+/+", true)]
        [InlineData("abc/def", "abc/def/#", true)]
        [InlineData("abc/def", "abc/defg/#", false)]
        [InlineData("abc/defg", "abc/def", false)]
        [InlineData("sports/tennis/player1", "sports/tennis/player1/#", true)]
        [InlineData("sports/tennis/player1/ranking", "sports/tennis/player1/#", true)]
        [InlineData("sports/tennis/player1/score/wimbledon", "sports/tennis/player1/#", true)]
        [InlineData("sports/tennis/player1", "sports/tennis/#", true)]
        [InlineData("sports", "sports/#", true)]
        [InlineData("sport/tennis/player1", "sport/tennis/+", true)]
        [InlineData("sport/tennis/", "sport/tennis/+", true)]
        [InlineData("sport/tennis", "sport/tennis/+", false)]
        [InlineData("sport/tennis/player1/ranking", "sport/tennis/+", false)]
        [InlineData("/a/bbb/c", "/a/+/c", true)]
        [InlineData("///", "+/+/+/", true)]
        [InlineData("/", "+", false)]
        [InlineData("/a", "+/a", true)]
        [InlineData("abc/a", "+/a", true)]
        [InlineData("a/a", "+/a", true)]
        [InlineData("/a/bc/c", "/a/+/c", true)]
        [InlineData("a", "#", true)]
        [InlineData("/", "#", true)]
        [InlineData("abc/def", "#", true)]
        public void MqttTopicMatchTest(string topicName, string topicFilter, bool expectedResult)
        {
            bool result = Util.CheckTopicFilterMatch(topicName, topicFilter);
            Assert.Equal(expectedResult, result);
        }

        [Fact]
        public void Experiment()
        {
            var template = new UriTemplate("devices/{deviceId}/messages/outbound/{*subTopic}");
            var baseUri = new Uri("http://whatever");
            Uri bound = template.BindByName(baseUri, new Dictionary<string, string>
            {
                { "deviceId", "VINno" },
                { "SubTopic", "toptop/toptoptop" },
            });
            var t2 = new UriTemplate("devices/{deviceId}/messages/log/{level=info}/{subject=n%2Fa}", true);
            UriTemplateMatch match = t2.Match(baseUri, new Uri("http://whatever/devices/VINno/messages/log", UriKind.Absolute));
        }
    }
}