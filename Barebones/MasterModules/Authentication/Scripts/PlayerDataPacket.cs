using System.Collections.Generic;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     This packet is sent to client on successful login
    /// </summary>
    public class PlayerDataPacket : SerializablePacket
    {
        public Dictionary<string, string> Data;
        public bool IsAdmin;
        public bool IsGuest;
        public bool IsEmailConfirmed;
        public string Token = "";
        public string Username;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Username);
            writer.Write(Token);
            writer.Write(IsAdmin);
            writer.Write(IsGuest);
            writer.Write(IsEmailConfirmed);

            Data.ToWriter(writer);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Username = reader.ReadString();
            Token = reader.ReadString();
            IsAdmin = reader.ReadBoolean();
            IsGuest = reader.ReadBoolean();
            IsEmailConfirmed = reader.ReadBoolean();

            Data = new Dictionary<string, string>().FromReader(reader);
        }
    }
}