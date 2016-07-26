namespace ProtocolGateway.Host.Fabric.FabricShared.Security
{
    #region Using Clauses
    using System;
    using System.Runtime.InteropServices;
    using System.Security;
    #endregion

    /// <summary>
    /// Contains common secure string handling methods.
    /// </summary>
    public static class SecureStringExtensions
    {
        #region Extension Methods
        /// <summary>
        /// Creates a secure string based on the contents of the provided string.
        /// </summary>
        /// <param name="value">The string holding the contents of the secure string.</param>
        /// <returns>A populated secure string that contains the value specified in the string parameter.</returns>
        public static SecureString GetSecureString(this string value)
        {
            var results = new SecureString();

            foreach (char character in value)
            {
                results.AppendChar(character);
            }

            return results;
        }

        /// <summary>
        /// Creates a CLR string based on the provided secure string value.
        /// </summary>
        /// <param name="value">The secure string that contains the value to be placed in the CLR string.</param>
        /// <returns></returns>
        public static string SecureStringToString(this SecureString value)
        {
            string plainValue = null;

            if (value != null)
            {
                IntPtr valuePtr = IntPtr.Zero;

                try
                {
                    valuePtr = Marshal.SecureStringToGlobalAllocUnicode(value);

                    plainValue = Marshal.PtrToStringUni(valuePtr);
                }
                finally
                {
                    if (valuePtr != IntPtr.Zero) Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
                }
            }

            return plainValue;
        }
        #endregion
    }
}

