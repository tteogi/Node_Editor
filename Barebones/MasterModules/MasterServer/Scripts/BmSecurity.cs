using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    /// <summary>
    /// Helper class, which implements means to encrypt and decrypt data
    /// </summary>
    public class BmSecurity
    {
        public static int RsaKeySize = 512;

        private static byte[] _salt = Encoding.ASCII.GetBytes("o6806642kbM7c5");

        private static string _clientAesKey;
        private static int _clientToMasterId;
        private static RSACryptoServiceProvider _clientsCsp;
        private static RSAParameters _clientsPublicKey;

        public static string ClientAesKey {get { return _clientAesKey; } }

        /// <summary>
        /// Should be called on client. Generates RSA public key, 
        /// sends it to master, which returns encrypted AES key. After decrypting AES key,
        /// callback is invoked with the value. You can then use the AES key to encrypt data
        /// </summary>
        /// <param name="callback"></param>
        public static void GetAesKey(Action<string> callback)
        {
            if (_clientsCsp == null)
            {
                _clientsCsp = new RSACryptoServiceProvider(RsaKeySize);

                // Generate keys
                _clientsPublicKey = _clientsCsp.ExportParameters(false);
            }

            var connectionPeer = Connections.ClientToMaster.Peer;

            if (_clientToMasterId == connectionPeer.Id && _clientAesKey != null)
            {
                // We already have an aes generated for this connection
                callback.Invoke(_clientAesKey);
                return;
            }

            // Serialize public key
            var sw = new StringWriter();
            var xs = new System.Xml.Serialization.XmlSerializer(typeof(RSAParameters));
            xs.Serialize(sw, _clientsPublicKey);

            // Send the request
            var msg = MessageHelper.Create(BmOpCodes.AesKeyRequest, sw.ToString());
            connectionPeer.SendMessage(msg, (status, response) =>
            {
                if (_clientToMasterId == connectionPeer.Id && _clientAesKey != null)
                {
                    // Aes is already decrypted.
                    callback.Invoke(_clientAesKey);
                    return;
                }

                if (status != AckResponseStatus.Success)
                {
                    // Failed to get an aes key
                    callback.Invoke(null);
                    return;
                }

                _clientToMasterId = connectionPeer.Id;
                var decrypted = _clientsCsp.Decrypt(response.AsBytes(), false);
                _clientAesKey = Encoding.Unicode.GetString(decrypted);

                callback.Invoke(_clientAesKey);
            });
        }

        public static byte[] EncryptAES(byte[] rawData, string sharedSecret)
        {
            using (var aesAlg = new RijndaelManaged())
            {
                // generate the key from the shared secret and the salt
                Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(sharedSecret, _salt);

                // Create a RijndaelManaged object
                aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);

                // Create a decryptor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    // prepend the IV
                    msEncrypt.Write(BitConverter.GetBytes(aesAlg.IV.Length), 0, sizeof(int));
                    msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length);
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, csEncrypt))
                        {
                            //Write all data to the stream.
                            writer.Write(rawData.Length);
                            writer.Write(rawData);
                        }
                    }
                    return msEncrypt.ToArray();
                }
            }
        }

        public static byte[] DecryptAES(byte[] encryptedData, string sharedSecret)
        {
            using (var aesAlg = new RijndaelManaged())
            {
                // generate the key from the shared secret and the salt
                Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(sharedSecret, _salt);

                using (MemoryStream msDecrypt = new MemoryStream(encryptedData))
                {
                    // Get the key
                    aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);
                    // Get the initialization vector from the encrypted stream
                    aesAlg.IV = ReadByteArray(msDecrypt);
                    // Create a decrytor to perform the stream transform.
                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (var reader = new EndianBinaryReader(EndianBitConverter.Big, csDecrypt))
                        {
                            return reader.ReadBytes(reader.ReadInt32());
                        }
                    }
                }
            }

        }

        /// <summary>
        /// Encrypt the given string using AES.  The string can be decrypted using 
        /// DecryptStringAES().  The sharedSecret parameters must match.
        /// </summary>
        /// <param name="plainText">The text to encrypt.</param>
        /// <param name="sharedSecret">A password used to generate a key for encryption.</param>
        public static string EncryptStringAES(string plainText, string sharedSecret)
        {
            string outStr = null;                       // Encrypted string to return
            RijndaelManaged aesAlg = null;              // RijndaelManaged object used to encrypt the data.

            try
            {
                // generate the key from the shared secret and the salt
                Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(sharedSecret, _salt);

                // Create a RijndaelManaged object
                aesAlg = new RijndaelManaged();
                aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);

                // Create a decryptor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    // prepend the IV
                    msEncrypt.Write(BitConverter.GetBytes(aesAlg.IV.Length), 0, sizeof(int));
                    msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length);
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            //Write all data to the stream.
                            swEncrypt.Write(plainText);
                        }
                    }
                    outStr = Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
            finally
            {
                // Clear the RijndaelManaged object.
                if (aesAlg != null)
                    aesAlg.Clear();
            }

            // Return the encrypted bytes from the memory stream.
            return outStr;
        }

        /// <summary>
        /// Decrypt the given string.  Assumes the string was encrypted using 
        /// EncryptStringAES(), using an identical sharedSecret.
        /// </summary>
        /// <param name="cipherText">The text to decrypt.</param>
        /// <param name="sharedSecret">A password used to generate a key for decryption.</param>
        public static string DecryptStringAES(string cipherText, string sharedSecret)
        {
            // Declare the RijndaelManaged object
            // used to decrypt the data.
            RijndaelManaged aesAlg = null;

            // Declare the string used to hold
            // the decrypted text.
            string plaintext = null;

            try
            {
                // generate the key from the shared secret and the salt
                Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(sharedSecret, _salt);

                // Create the streams used for decryption.                
                byte[] bytes = Convert.FromBase64String(cipherText);
                using (MemoryStream msDecrypt = new MemoryStream(bytes))
                {
                    // Create a RijndaelManaged object
                    // with the specified key and IV.
                    aesAlg = new RijndaelManaged();
                    aesAlg.Key = key.GetBytes(aesAlg.KeySize / 8);
                    // Get the initialization vector from the encrypted stream
                    aesAlg.IV = ReadByteArray(msDecrypt);
                    // Create a decrytor to perform the stream transform.
                    ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))

                            // Read the decrypted bytes from the decrypting stream
                            // and place them in a string.
                            plaintext = srDecrypt.ReadToEnd();
                    }
                }
            }
            finally
            {
                // Clear the RijndaelManaged object.
                if (aesAlg != null)
                    aesAlg.Clear();
            }

            return plaintext;
        }

        private static byte[] ReadByteArray(Stream s)
        {
            byte[] rawLength = new byte[sizeof(int)];
            if (s.Read(rawLength, 0, rawLength.Length) != rawLength.Length)
            {
                throw new SystemException("Stream did not contain properly formatted byte array");
            }

            byte[] buffer = new byte[BitConverter.ToInt32(rawLength, 0)];
            if (s.Read(buffer, 0, buffer.Length) != buffer.Length)
            {
                throw new SystemException("Did not read byte array properly");
            }

            return buffer;
        }
    }
}