using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class SpawnPlayerManager : NetworkBehaviour
{
    public static SpawnPlayerManager Instance;
    private readonly List<int> usedSpawnIndexes = new();
    [Header("Setup")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private GameObject playerPrefab;

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;

            NetworkManager.Singleton.OnServerStopped -= ClearAllEvents;
            NetworkManager.Singleton.OnServerStopped += ClearAllEvents;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        Transform spawnPoint = GetAvailableSpawnPoint();
        if (spawnPoint == null)
        {
            return;
        }

        GameObject player = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        var netObj = player.GetComponent<NetworkObject>();
        netObj.SpawnAsPlayerObject(clientId, true);

        usedSpawnIndexes.Add(System.Array.IndexOf(spawnPoints, spawnPoint));
    }

    private Transform GetAvailableSpawnPoint()
    {
        foreach (var point in spawnPoints)
        {
            int index = System.Array.IndexOf(spawnPoints, point);
            if (!usedSpawnIndexes.Contains(index))
            {
                return point;
            }
        }
        return null;
    }

    public Vector3 GetSpawnPosition(ulong clientId)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
            return Vector3.zero;

        int index = (int)(clientId % (ulong)spawnPoints.Length);
        return spawnPoints[index].position;
    }

    private void ClearAllEvents(bool isHost)
    {
        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnServerStopped -= ClearAllEvents;
    }

    private new void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnServerStopped -= ClearAllEvents;
        }
    }
}
