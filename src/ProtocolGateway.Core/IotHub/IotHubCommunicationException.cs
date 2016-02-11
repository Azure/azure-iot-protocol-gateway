// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Azure.Devices.ProtocolGateway.IotHub
{
    using System;
    public class IotHubCommunicationException: Exception
    {

        public bool IsTransient { get; }

        public string TrackingId { get; set; }

        public IotHubCommunicationException(string message)
            : this(message, false)
        {
        }

        public IotHubCommunicationException(string message, bool isTransient)
            : this(message, isTransient, string.Empty)
        {
        }

        public IotHubCommunicationException(string message, string trackingId)
            : this(message, false, trackingId)
        {
        }

        public IotHubCommunicationException(string message, bool isTransient, string trackingId)
            : base(message)
        {
            this.IsTransient = isTransient;
            this.TrackingId = trackingId;
        }

        public IotHubCommunicationException(string message, Exception innerException)
            : this(message, innerException, false)
        {
        }

        public IotHubCommunicationException(string message, Exception innerException, bool isTransient)
            : this(message, innerException, isTransient, string.Empty)
        {
        }

        public IotHubCommunicationException(string message, Exception innerException, string trackingId)
            : this(message, innerException, false, trackingId)
        {
        }

        public IotHubCommunicationException(string message, Exception innerException, bool isTransient, string trackingId)
            : base(message, innerException)
        {
            this.IsTransient = isTransient;
            this.TrackingId = trackingId;
        }
    }
}