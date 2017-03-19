using System;
using System.Security.Cryptography;
using System.Text;
using Barebones.Networking;
using Barebones.Utils;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    /// Handles a request from user to return an encrypted AES key
    /// </summary>
    public class AesKeyRequestHandler : IPacketHandler
    {
        public short OpCode { get { return BmOpCodes.AesKeyRequest; } }

        public void Handle(IIncommingMessage message)
        {
            var encryptedKey = message.Peer.GetProperty(BmPropCodes.AesKeyEncrypted) as byte[];

            if (encryptedKey != null)
            {
                // There's already a key generated
                message.Respond(encryptedKey, AckResponseStatus.Success);
                return;
            }

            // Generate a random key
            var aesKey = BmHelper.CreateRandomString(8);

            var clientsPublicKeyXml = message.AsString();

            // Deserialize public key
            var sr = new System.IO.StringReader(clientsPublicKeyXml);
            var xs = new System.Xml.Serialization.XmlSerializer(typeof(RSAParameters));
            var clientsPublicKey = (RSAParameters)xs.Deserialize(sr);

            using (var csp = new RSACryptoServiceProvider())
            {
                csp.ImportParameters(clientsPublicKey);
                var encryptedAes = csp.Encrypt(Encoding.Unicode.GetBytes(aesKey), false);

                // Save keys as peer properties for later use
                message.Peer.SetProperty(BmPropCodes.AesKeyEncrypted, encryptedAes);
                message.Peer.SetProperty(BmPropCodes.AesKey, aesKey);

                message.Respond(encryptedAes, AckResponseStatus.Success);
            }

        }
    }
}