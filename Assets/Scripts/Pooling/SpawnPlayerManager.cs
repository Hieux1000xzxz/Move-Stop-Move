using Unity.Netcode;
using UnityEngine;
using System.Collections;
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
            usedSpawnIndexes.Clear();

            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;

            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

            NetworkManager.Singleton.OnServerStopped -= ClearAllEvents;
            NetworkManager.Singleton.OnServerStopped += ClearAllEvents;
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        StartCoroutine(SpawnAfterSync(clientId));
    }

    private IEnumerator SpawnAfterSync(ulong clientId)
    {
        yield return null;

        // Nếu client đã có player thì không spawn lại
        if (NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId)
            && NetworkManager.Singleton.ConnectedClients[clientId].PlayerObject != null)
        {
            yield break;
        }

        Transform spawnPoint = GetAvailableSpawnPoint();
        if (spawnPoint == null)
            yield break;

        string selectedName = PlayerPrefs.GetString("SelectedCharacter", "");
        CharacterData selected = null;
        foreach (var c in GameManager.Instance.AllCharacters)
        {
            if (c != null && c.Name == selectedName)
            {
                selected = c;
                break;
            }
        }

        if (selected == null)
        {
            selected = GameManager.Instance.DefaultCharacter;
        }

        GameObject player = Instantiate(selected.Prefab, spawnPoint.position, spawnPoint.rotation);
        var netObj = player.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Destroy(player);
            yield break;
        }

        netObj.SpawnAsPlayerObject(clientId, true);
        usedSpawnIndexes.Add(System.Array.IndexOf(spawnPoints, spawnPoint));

        Debug.Log($"✅ Spawned player {selected.Name} for client {clientId}");
    }


    private void HandleClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        if (usedSpawnIndexes.Count > 0)
        {
            int index = (int)(clientId % (ulong)spawnPoints.Length);
            if (index >= 0 && index < spawnPoints.Length)
            {
                usedSpawnIndexes.Remove(index);
            }
        }
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
        usedSpawnIndexes.Clear();

        if (NetworkManager.Singleton == null) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        NetworkManager.Singleton.OnServerStopped -= ClearAllEvents;
    }
}
