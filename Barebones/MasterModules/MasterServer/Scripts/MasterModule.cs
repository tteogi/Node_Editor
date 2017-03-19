using System;
using System.Collections.Generic;
using System.Linq;
using Barebones.Logging;
using UnityEngine;

namespace Barebones.MasterServer
{
    /// <summary>
    ///     Represents a base class for master server modules
    /// </summary>
    public abstract class MasterModule : MonoBehaviour, IMasterModule
    {
        private static Dictionary<Type, GameObject> _instances;
        
        private readonly List<Type> _dependencies = new List<Type>();

        /// <summary>
        ///     Returns a list of module types this module depends on
        /// </summary>
        public IEnumerable<Type> Dependencies
        {
            get { return _dependencies; }
        }

        /// <summary>
        ///     Called by master server, when module should be started
        /// </summary>
        /// <param name="master"></param>
        public abstract void Initialize(IMaster master);

        /// <summary>
        ///     Adds a dependency to list. Should be called in Awake or Start method's of
        ///     module
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void AddDependency<T>()
        {
            _dependencies.Add(typeof(T));
        }

        /// <summary>
        /// Returns true, if module should be destroyed
        /// </summary>
        /// <returns></returns>
        protected bool DestroyIfExists()
        {
            if (_instances == null)
                _instances = new Dictionary<Type, GameObject>();

            if (_instances.ContainsKey(GetType()))
            {
                if (_instances[GetType()] != null)
                {
                    // Module hasn't been destroyed
                    Destroy(gameObject);
                    return true;
                }

                // Remove an old module, which has been destroyed previously
                // (probably automatically when changing a scene)
                _instances.Remove(GetType());
            }

            _instances.Add(GetType(), gameObject);
            return false;
        }
    }
}