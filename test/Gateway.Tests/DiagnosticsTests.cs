// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Tests
{
    using global::Gateway.Samples.Common;
    using Microsoft.Azure.Devices.Gateway.Core;
    using Microsoft.Practices.EnterpriseLibrary.SemanticLogging.Utility;
    using Xunit;

    public class DiagnosticsTests
    {
        [Fact]
        public void VerifyEventSources()
        {
            EventSourceAnalyzer.InspectAll(BridgeEventSource.Log);
            EventSourceAnalyzer.InspectAll(BootstrapperEventSource.Log);
        }
    }
}