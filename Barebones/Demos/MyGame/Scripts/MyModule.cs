
using Barebones.MasterServer;
using Barebones.Networking;
using UnityEngine;

/// <summary>
/// Our custom module
/// </summary>
public class MyModule : MasterModule
{
    private AuthModule _auth;
    private IMaster _master;

    void Awake()
    {
        // Register dependency. Initialize method will only be called
        // when modules in dependency list have been initialized
        AddDependency<AuthModule>();
    }

    /// <summary>
    /// Called, when all dependencies are met and master server is about to start
    /// </summary>
    public override void Initialize(IMaster master)
    {
        _master = master;
        _auth = master.GetModule<AuthModule>();
        
        // Add client message handlers,
        _master.SetClientHandler(new PacketHandler(MyOpCodes.GetPersonalInfo, HandleGetInfo));
        _master.SetClientHandler(new PacketHandler(MyOpCodes.SavePersonalInfo, HandleSaveInfo));

        // Listen to login event in the auth module
        _auth.OnLogin += OnLogin;

        Logs.Debug("My module has been initialized");
    }

    protected virtual void OnLogin(ISession session, IAccountData data)
    {
        Logs.Debug("MyModule was informed about a user who logged in");
    }

    private void HandleGetInfo(IIncommingMessage message)
    {
        var info = message.Peer.GetProperty(MyPropCodes.PersonalInfo) as PersonalInfoPacket;

        if (info == null)
        {
            // If there's no info
            message.Respond("You have no profile info", AckResponseStatus.Failed);
            return;
        }

        // We found the info
        message.Respond(info, AckResponseStatus.Success);
    }

    private void HandleSaveInfo(IIncommingMessage message)
    {
        // Deserialize packet
        var info = message.DeserializePacket(new PersonalInfoPacket());

        // Update the property value
        message.Peer.SetProperty(MyPropCodes.PersonalInfo, info);

        // Get the session (for no reason)
        var session = message.Peer.GetProperty(BmPropCodes.Session) as ISession;

        Logs.Debug("Server saved personal info from user: " + session.Username);
    }

    /// <summary>
    /// Collection of your operation codes. 
    /// </summary>
    public class MyOpCodes
    {
        public const short GetPersonalInfo = 0;
        public const short SavePersonalInfo = 1;
    }

    /// <summary>
    /// Collection of peer property codes
    /// </summary>
    public class MyPropCodes
    {
        public const int PersonalInfo = 0;
    }

    /// <summary>
    /// A simple packet, which represents personal info of the player
    /// </summary>
    public class PersonalInfoPacket : SerializablePacket
    {
        public string Name;
        public int Age;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(Age);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Name = reader.ReadString();
            Age = reader.ReadInt32();
        }
    }

    public static void SavePersonalInfo(int age, string name)
    {
        // Construct the packet
        var info = new PersonalInfoPacket()
        {
            Age = age,
            Name = name
        };

        // Create a message
        var msg = MessageHelper.Create(MyOpCodes.SavePersonalInfo, info.ToBytes());

        // Send a reliable message to master server
        Connections.ClientToMaster.Peer.SendMessage(msg, DeliveryMethod.Reliable);
    }

    public static void GetPersonalInfo()
    {
        // Create an empty request message
        var msg = MessageHelper.Create(MyOpCodes.GetPersonalInfo);

        // Send message to master server, and wait for the response
        Connections.ClientToMaster.Peer.SendMessage(msg, (status, response) =>
        {
            // Response received

            if (status != AckResponseStatus.Success)
            {
                // If request failed
                Logs.Error(msg.ToString());
                return;
            }

            // Success
            var info = response.DeserializePacket(new PersonalInfoPacket());

            Logs.Debug(string.Format("I've got info. Name: {0}, Age: {1}", info.Name, info.Age));
        });
    }
}
