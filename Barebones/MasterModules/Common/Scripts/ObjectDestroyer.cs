using UnityEngine;

namespace Barebones.MasterServer
{
    public class ObjectDestroyer : MonoBehaviour
    {
        void Awake()
        {
            if (BmArgs.DestroyObjects)
            {
                Destroy(gameObject);
            }
        }
    }
}