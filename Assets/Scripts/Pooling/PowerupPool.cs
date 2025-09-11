using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PowerupPool : MonoBehaviour
{
    public static PowerupPool Instance;

    [System.Serializable]
    public class PowerupEntry
    {
        public PowerupType type;
        public GameObject prefab;
        public int prewarmCount = 5;
    }

    [SerializeField] private List<PowerupEntry> entries;

    private Dictionary<PowerupType, Queue<GameObject>> pool = new();

    private void Awake()
    {
        Instance = this;

        foreach (var entry in entries)
        {
            pool[entry.type] = new Queue<GameObject>();
            for (int i = 0; i < entry.prewarmCount; i++)
            {
                GameObject obj = CreateNew(entry.prefab, entry.type);
                obj.SetActive(false);
                pool[entry.type].Enqueue(obj);
            }
        }
    }

    private GameObject CreateNew(GameObject prefab, PowerupType type)
    {
        GameObject obj = Instantiate(prefab, transform);
        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"Prefab {prefab.name} thiếu NetworkObject!");
        }
        obj.SetActive(false);
        return obj;
    }

    public GameObject Spawn(PowerupType type, Vector3 pos, Quaternion rot)
    {
        if (!pool.ContainsKey(type))
            return null;

        GameObject obj;
        if (pool[type].Count > 0)
        {
            obj = pool[type].Dequeue();
        }
        else
        {
            var entry = entries.Find(e => e.type == type);
            obj = CreateNew(entry.prefab, type);
        }

        obj.transform.position = pos;
        obj.transform.rotation = rot;
        obj.SetActive(true);

        var netObj = obj.GetComponent<NetworkObject>();
        if (!netObj.IsSpawned && NetworkManager.Singleton.IsServer)
        {
            netObj.Spawn(true); // spawn cho client thấy
        }

        return obj;
    }

    public void Release(GameObject obj, PowerupType type)
    {
        if (obj == null) return;

        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned && NetworkManager.Singleton.IsServer)
        {
            netObj.Despawn(false); // ko destroy, chỉ ẩn
        }

        obj.SetActive(false);
        obj.transform.SetParent(transform);
        pool[type].Enqueue(obj);
    }
}
