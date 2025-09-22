using Unity.Netcode;
using UnityEngine;

public class RuntimePrefabRegister : MonoBehaviour
{
    [SerializeField] private GameObject[] prefabsToRegister;

    private void Awake()
    {
        var networkConfig = NetworkManager.Singleton.NetworkConfig;

        foreach (var prefab in prefabsToRegister)
        {
            if (prefab.TryGetComponent<NetworkObject>(out var netObj))
            {
                var entry = new NetworkPrefab();
                entry.Prefab = prefab;

                // tránh thêm trùng prefab
                if (!networkConfig.Prefabs.Contains(entry))
                {
                    networkConfig.Prefabs.Add(entry);
                    Debug.Log($"[Netcode] Registered prefab (legacy): {prefab.name}");
                }
            }
        }
    }
}
