using System;
using System.Collections.Generic;

namespace Barebones.MasterServer
{
    public interface IMasterModule
    {
        IEnumerable<Type> Dependencies { get; }

        /// <summary>
        ///     Called when master servers starts, or when you add the module,
        ///     if the master is already starterd
        /// </summary>
        void Initialize(IMaster master);
    }
}