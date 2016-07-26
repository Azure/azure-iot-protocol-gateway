namespace ProtocolGateway.Host.Fabric.FrontEnd.Configuration
{
    #region Using Clauses
    using System;
    #endregion

    /// <summary>
    /// Configuration information for the protocol gateway
    /// </summary>
    public sealed class GatewayConfiguration
    {
        /// <summary>
        /// The thumbprint of the certificate if in keyvault or local, file name if data store to use for server side MQTTS
        /// </summary>
        public string X509Identifier { get; set; }


        /// Todo: Secure this

        /// <summary>
        /// The file password, client credential for key vault for the server certificate
        /// </summary>
        public string X509Credential { get; set; }

        /// <summary>
        /// The location that the certificate is stored in
        /// </summary>
        public CertificateLocation X509Location { get; set; }

        /// <summary>
        /// The name of the endpoint in the endpoint list
        /// </summary>
        public string EndPointName { get; set; }

        /// <summary>
        /// The interval to report health at
        /// </summary>
        public TimeSpan HealthReportInterval { get; set; }

        /// <summary>
        /// The interval to report capacity metrics at
        /// </summary>
        public TimeSpan MetricsReportInterval { get; set; }

        /// <summary>
        /// The possible locations for the X509 certificate to be stored
        /// </summary>
        public enum CertificateLocation
        {
            LocalStore = 0,
            Data = 1,
            KeyVault = 2
        }
    }
}
