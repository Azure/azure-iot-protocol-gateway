// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.IotHub
{
    using System.Collections.Generic;

    public interface IMetadataSerializer
    {
        IDictionary<string, string> Deserialize(string data);

        string Deserialize(IDictionary<string, string> data);
    }
}