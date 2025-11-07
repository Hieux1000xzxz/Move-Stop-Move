using System.Collections;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;

public partial class ConnectionCanvas
{
     #region Networking
    private void OnServerStarted()
    {
        Debug.Log("Server started successfully");
    }
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected");
        if (pollLobbyRoutine != null) StopCoroutine(pollLobbyRoutine);
        pollLobbyRoutine = StartCoroutine(PollLobbyInfo());
    }
    private void OnClientDisconnected(ulong clientId)
    {
        Debug.LogWarning($"Client {clientId} disconnected. IsServer={NetworkManager.Singleton.IsServer}");

        if (!isIntentionalDisconnect && GameManager.Instance.IsGameStarted &&
            !NetworkManager.Singleton.IsServer &&
            !NetworkManager.Singleton.IsHost &&
            !NetworkManager.Singleton.ShutdownInProgress)
        {
            if (gameplayCanvas != null)
            {
                gameplayCanvas.OnExitGame(true);
            }
            else
            {
                UIManager.Instance.SendNotification("Host has left the room. The game has ended.", 1);
                UIManager.Instance.OpenNotification();
                HandleClientDisconnect();
            }
            return;
        }

        if (!NetworkManager.Singleton.ShutdownInProgress)
        {
            StartCoroutine(PollLobbyInfo());
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            CleanupPlayerObjects(clientId);
        }
        else if (!isIntentionalDisconnect)
        {
            HandleClientDisconnect();
        }
    }
    private void CleanupPlayerObjects(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (NetworkManager.Singleton.SpawnManager == null) return;

        foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
            if (netObj != null && netObj.OwnerClientId == clientId)
            {
                netObj.Despawn();
            }
        }
    }
    private IEnumerator SendHeartbeatRoutine()
    {
        while (networkManager != null && networkManager.IsHost && !networkManager.ShutdownInProgress)
        {
            using (var www = UnityWebRequest.PostWwwForm($"{SERVER_URL}/{currentLobbyId}/heartbeat", ""))
            {
                www.timeout = 10;
                yield return www.SendWebRequest();
            }
            yield return new WaitForSeconds(5f);
        }
    }
    private IEnumerator SendClientHeartbeatRoutine()
    {
        while (networkManager != null && networkManager.IsClient && !networkManager.ShutdownInProgress)
        {
            if (!string.IsNullOrEmpty(currentLobbyId))
            {
                string playerId = PlayerPrefs.GetString("PlayerId", "");
                if (!string.IsNullOrEmpty(playerId))
                {
                    var payload = new ClientHeartbeatRequest { UserId = playerId };
                    string json = JsonUtility.ToJson(payload);

                    using (var www = new UnityWebRequest($"{SERVER_URL}/{currentLobbyId}/client-heartbeat", "POST"))
                    {
                        www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                        www.downloadHandler = new DownloadHandlerBuffer();
                        www.SetRequestHeader("Content-Type", "application/json");
                        www.timeout = 10;

                        yield return www.SendWebRequest();
                    }
                }
            }
            yield return new WaitForSeconds(5f);
        }
        
        
    }
    #endregion
    
    private UnityWebRequest CreatePostRequest(string url, object payload)
    {
        string json = JsonUtility.ToJson(payload);
        var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 10;
        return req;
    }

}
