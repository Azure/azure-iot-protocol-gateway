namespace ProtocolGateway.Host.Fabric.FabricShared.Security
{
    #region Using Clauses
    using System;
    using System.Security;
    using System.Security.Cryptography.X509Certificates;
    #endregion

    /// <summary>
    /// Utilities associated with the X509 Certificates and their store
    /// </summary>
    public static class CertificateUtilities
    {
        #region Certificate Retrieval
        /// <summary>
        /// Retrieves an X509 Certificate from the specified store and location
        /// </summary>
        /// <param name="thumbprint">The certificate thumbprint</param>
        /// <param name="storeName">The name of the store to retrieve the information from</param>
        /// <param name="storeLocation">The location within the store where the certificate is located</param>
        /// <returns>An X509 certificate with the specified thumbprint if available or null if not</returns>
        public static X509Certificate2 GetCertificate(string thumbprint, StoreName storeName, StoreLocation storeLocation)
        {
            X509Store store = null;
            X509Certificate2 certificate;

            try
            {
                store = new X509Store(storeName, storeLocation);
                store.Open(OpenFlags.ReadOnly);
                X509Certificate2Collection collection = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);

                certificate = collection.Count == 0 ? null : collection[0];
            }
            finally
            {
                store?.Close();
            }

            return certificate;
        }

        /// <summary>
        /// Retrieves an X509 certificate from a password protected PFX file file
        /// </summary>
        /// <returns>The X509 certificate</returns>
        public static X509Certificate2 GetCertificateFromFile(string fileName, SecureString password)
        {
            return new X509Certificate2(fileName, password);
        }

        /// <summary>
        /// Retrieves an X509 certificate from a password protected PFX file file
        /// </summary>
        /// <returns>The X509 certificate</returns>
        public static X509Certificate2 GetCertificateFromFile(string fileName, string password)
        {
            return new X509Certificate2(fileName, password);
        }

        /// <summary>
        /// Retrieves an X509 certificate from key vault
        /// </summary>
        /// <returns>The X509 certificate</returns>
        public static X509Certificate2 GetCertificateFromKeyVault(string fileName, SecureString password)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
