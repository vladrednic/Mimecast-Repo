using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace A2A.Option {
    public static class SymmetricEncryption {

        #region Encryption Constants
        private const String DefaultPassword = "A Reasonably Long string SHOULD Be Used here";

        private static readonly byte[] InitialVector = { 27, 53, 232, 158, 143, 38, 133, 48, 245, 198, 45, 75, 151, 137, 17, 192 };
        private static readonly byte[] Salt = { 11, 8, 244, 117, 154, 131, 138, 40, 170, 56, 128, 143, 105, 61, 167, 61 };

        // The minimum recommended number of iterations is 1000.
        private const int KeyIterations = 2000;
        // The key size must be 128, 192, or 256 bits (but is specified in bytes).
        private const int RijndaelKeySize = 256 / 8;
        #endregion

        #region Static Functions


        /// <summary>
        /// Encrypts the specified plain text.
        /// </summary>
        /// <param name="plainText">The plain text.</param>
        /// <param name="password">The password.</param>
        /// <param name="keyIterations">The number key iterations (should be greater than 1000).</param>
        /// <returns>A string consisting of the Base64 encoded bytes</returns>
        public static string Encrypt(string plainText, string password = DefaultPassword, int keyIterations = KeyIterations) {
            if (string.IsNullOrEmpty(plainText)) return "";

            var derivedPassword = new Rfc2898DeriveBytes(password, Salt, keyIterations);
            var keyBytes = derivedPassword.GetBytes(RijndaelKeySize);

            return Encrypt(plainText, keyBytes, InitialVector);
        }
        /// <summary>
        /// Decrypts a string
        /// </summary>
        /// <param name="base64String">Base64 encoded bytes to be decrypted</param>
        /// <param name="password">Password to decrypt with</param>
        /// <param name="keyIterations">Number of iterations to do</param>
        /// <returns>A decrypted string</returns>
        public static string Decrypt(string base64String, string password = DefaultPassword, int keyIterations = KeyIterations) {
            if (String.IsNullOrEmpty(base64String)) return "";

            var derivedPassword = new Rfc2898DeriveBytes(password, Salt, keyIterations);
            var keyBytes = derivedPassword.GetBytes(RijndaelKeySize);

            return Decrypt(base64String, keyBytes, InitialVector);
        }

        private static string Encrypt(string plainText, byte[] key, byte[] vector) {
            // Check arguments. 
            if (string.IsNullOrEmpty(plainText))
                throw new ArgumentNullException("plainText");
            if (key == null || key.Length <= 0)
                throw new ArgumentNullException("key");
            if (vector == null || vector.Length <= 0)
                throw new ArgumentNullException("vector");

            byte[] encrypted;
            using (var symmetricKey = new AesCryptoServiceProvider { Padding = PaddingMode.Zeros, Mode = CipherMode.CBC }) {
                using (var encryptor = symmetricKey.CreateEncryptor(key, vector)) {
                    var memStream = new MemoryStream();
                    var cryptoStream = new CryptoStream(memStream, encryptor, CryptoStreamMode.Write);

                    using (var writer = new StreamWriter(cryptoStream)) {
                        writer.Write(plainText);
                    }
                    encrypted = memStream.ToArray();
                }
            }
            return Convert.ToBase64String(encrypted);
        }

        private static string Decrypt(string base64String, byte[] key, byte[] vector) {
            // Check arguments. 
            if (string.IsNullOrEmpty(base64String))
                throw new ArgumentNullException("base64String");
            if (key == null || key.Length <= 0)
                throw new ArgumentNullException("key");
            if (vector == null || vector.Length <= 0)
                throw new ArgumentNullException("vector");
            string plainText;
            try {
                var cipherBytes = Convert.FromBase64String(base64String);
                using (var symmetricKey = new AesCryptoServiceProvider { Padding = PaddingMode.Zeros, Mode = CipherMode.CBC }) {
                    using (var decryptor = symmetricKey.CreateDecryptor(key, vector)) {
                        var memStream = new MemoryStream(cipherBytes);
                        var cryptoStream = new CryptoStream(memStream, decryptor, CryptoStreamMode.Read);
                        using (var reader = new StreamReader(cryptoStream)) {
                            plainText = reader.ReadToEnd();
                        }
                    }
                }
            }
            catch {
                plainText = base64String;
            }
            return plainText.Replace("\0", null);
        }


        /// <summary>
        /// Decrypts a string
        /// </summary>
        /// <param name="base64String">Base64 encoded bytes to be decrypted</param>
        /// <param name="password">Password to decrypt with</param>
        /// <param name="keyIterations">Number of iterations to do</param>
        /// <returns>A decrypted string</returns>
        public static SecureString DecryptToSecureString(string base64String, string password = DefaultPassword, int keyIterations = KeyIterations) {
            if (string.IsNullOrEmpty(base64String)) return new SecureString();

            var derivedPassword = new Rfc2898DeriveBytes(password, Salt, keyIterations);
            var keyBytes = derivedPassword.GetBytes(RijndaelKeySize);

            return DecryptToSecureString(base64String, keyBytes, InitialVector);
        }

        private static SecureString DecryptToSecureString(string base64String, byte[] key, byte[] vector) {
            // Check arguments. 
            if (string.IsNullOrEmpty(base64String))
                throw new ArgumentNullException("base64String");
            if (key == null || key.Length <= 0)
                throw new ArgumentNullException("key");
            if (vector == null || vector.Length <= 0)
                throw new ArgumentNullException("vector");

            var cipherBytes = Convert.FromBase64String(base64String);
            var secureString = new SecureString();
            char[] chars = new char[1];
            using (var symmetricKey = new AesCryptoServiceProvider { Padding = PaddingMode.Zeros }) {
                using (var decryptor = symmetricKey.CreateDecryptor(key, vector)) {
                    var memStream = new MemoryStream(cipherBytes);
                    var cryptoStream = new CryptoStream(memStream, decryptor, CryptoStreamMode.Read);
                    using (var reader = new StreamReader(cryptoStream)) {
                        while (reader.Read(chars, 0, 1) == 1) {
                            secureString.AppendChar(chars[0]);
                        }
                    }
                }
            }
            return secureString;
        }
        #endregion

        #region Public Methods
        public static string GetMD5HashString(string inString) {
            return GetByteArrayAsString(GetMD5Hash(inString));
        }

        public static string GetByteArrayAsString(byte[] array) {
            if (array == null || array.Length == 0)
                return null;
            StringBuilder sb = new StringBuilder(array.Length * 2);
            foreach (byte b in array) {
                sb.AppendFormat("{0:X2}", b);
            }
            return sb.ToString();
        }

        public static byte[] GetMD5Hash(string inString) {
            StringBuilder sb = new StringBuilder();
            using (MD5CryptoServiceProvider md5Provider = new MD5CryptoServiceProvider()) {
                byte[] sBytes = Encoding.ASCII.GetBytes(inString);
                return md5Provider.ComputeHash(sBytes);
            }
        }
        #endregion Public Methods
    }
}
