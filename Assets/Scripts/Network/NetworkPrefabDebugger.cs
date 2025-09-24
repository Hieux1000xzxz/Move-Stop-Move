using Unity.Netcode;
using UnityEngine;

public class NetworkPrefabDebugger : MonoBehaviour
{
    private void Start()
    {
        var prefabs = NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs;
        foreach (var entry in prefabs)
        {
            if (entry.Prefab != null)
            {
                
                uint hash = entry.SourcePrefabGlobalObjectIdHash;
                Debug.Log($"Prefab: {entry.Prefab.name} | Hash: {hash}");
            }
        }
    }
}
