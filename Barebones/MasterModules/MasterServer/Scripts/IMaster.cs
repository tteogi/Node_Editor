using System;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    public interface IMaster
    {
        /// <summary>
        ///     When game servers register, they will need to match the master key
        /// </summary>
        string MasterKey { get; set; }

        /// <summary>
        /// Connected clients session registry
        /// </summary>
        SessionRegistry<ISession> SessionRegistry { get; }

        /// <summary>
        ///     Invoked when connection with game server is lost
        /// </summary>
        event Action<IPeer> OnClientDisconnected;

        /// <summary>
        ///     Invoked when game server connected to master
        /// </summary>
        event Action<IPeer> OnClientConnected;

        /// <summary>
        /// Adds a handler to the collection of client packet handlers.
        /// This handler will be invoked when master server receives a
        /// message of the specified <see cref="IPacketHandler.OpCode"/>
        /// </summary>
        /// <param name="handler"></param>
        IPacketHandler SetClientHandler(IPacketHandler handler);

        /// <summary>
        /// Adds a handler to the collection of client packet handlers.
        /// This handler will be invoked when master server receives a
        /// message of the specified opcode
        /// </summary>
        IPacketHandler SetClientHandler(short opCode, Action<IIncommingMessage> handler);

        [Obsolete("Use SetClientHandler")]
        IPacketHandler AddClientHandler(IPacketHandler handler);

        /// <summary>
        ///     Retrieves a module of type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T GetModule<T>() where T : class, IMasterModule;

        /// <summary>
        /// Adds a module, and initializes it (if master server has already started)
        /// </summary>
        /// <param name="module"></param>
        void AddAndInitializeModule(IMasterModule module);
    }
}