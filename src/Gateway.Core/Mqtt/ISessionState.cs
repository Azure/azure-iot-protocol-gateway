// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Gateway.Core.Mqtt
{
    using System.Collections.Generic;

    public interface ISessionState
    {
        List<Subscription> Subscriptions { get; }

        bool IsTransient { get; }

        ISessionState Copy();
    }
}