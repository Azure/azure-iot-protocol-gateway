// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public sealed class TestScenarioStep
    {
        TestScenarioStep()
        {
        }

        internal IEnumerable<object> Messages { get; private set; }

        internal bool WaitForFeedback { get; private set; }

        internal TimeSpan Delay { get; private set; }

        public static TestScenarioStep Write(params object[] messages)
        {
            return Write(true, messages);
        }

        public static TestScenarioStep Write(bool waitForFeedback, params object[] messages)
        {
            return new TestScenarioStep
            {
                Messages = messages ?? Enumerable.Empty<object>(),
                WaitForFeedback = waitForFeedback
            };
        }

        public static TestScenarioStep ReadMore()
        {
            return new TestScenarioStep
            {
                Messages = Enumerable.Empty<object>(),
                WaitForFeedback = true
            };
        }

        public static TestScenarioStep Wait(TimeSpan waitTime)
        {
            return new TestScenarioStep
            {
                Messages = Enumerable.Empty<object>(),
                Delay = waitTime,
                WaitForFeedback = false
            };
        }
    }
}