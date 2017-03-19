﻿using System.Collections.Generic;
using Barebones.Networking;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    /// Information about a member of the lobby
    /// </summary>
    public class LobbyMemberData : SerializablePacket
    {
        public string Name;
        public Dictionary<string, string> Properties;
        public bool IsReady;
        public string Team;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.WriteDictionary(Properties);
            writer.Write(IsReady);
            writer.Write(Name);
            writer.Write(Team);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Properties = reader.ReadDictionary();
            IsReady = reader.ReadBoolean();
            Name = reader.ReadString();
            Team = reader.ReadString();
        }
    }
}