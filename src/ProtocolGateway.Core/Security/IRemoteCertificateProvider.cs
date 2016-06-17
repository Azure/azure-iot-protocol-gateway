// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.ProtocolGateway.Security
{
    using System.Security.Cryptography.X509Certificates;

    public interface IRemoteCertificateProvider
    {
        X509Certificate GetRemoteCertificate();
    }
}