using System.Collections;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;

public partial class ConnectionCanvas
{
    #region Networking

    private void OnClientConnected(ulong clientId)
    {
        RestartLobbyPolling();
    }

    private void RestartLobbyPolling()
    {
        if (pollLobbyRoutine != null)
            StopCoroutine(pollLobbyRoutine);
        pollLobbyRoutine = StartCoroutine(PollLobbyInfo());
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (IsUnexpectedDisconnection())
        {
            HandleUnexpectedDisconnect();
            return;
        }

        if (!NetworkManager.Singleton.ShutdownInProgress)
            StartCoroutine(PollLobbyInfo());
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            CleanupPlayerObjects(clientId);
        else if (!isIntentionalDisconnect)
            HandleClientDisconnect();
    }

    private bool IsUnexpectedDisconnection()
    {
        return !isIntentionalDisconnect &&
               GameManager.Instance.IsGameStarted &&
               !NetworkManager.Singleton.IsServer &&
               !NetworkManager.Singleton.IsHost &&
               !NetworkManager.Singleton.ShutdownInProgress;
    }

    private void HandleUnexpectedDisconnect()
    {
        if (gameplayCanvas != null)
            gameplayCanvas.OnExitGame(true);
        else
        {
            UIManager.Instance.SendNotification("Host has left the room. The game has ended.", 1);
            UIManager.Instance.OpenNotification();
            HandleClientDisconnect();
        }
    }

    private void CleanupPlayerObjects(ulong clientId)
    {
        if (!CanCleanup()) return;

        foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
            if (netObj != null && netObj.OwnerClientId == clientId)
                netObj.Despawn();
        }
    }

    private bool CanCleanup()
    {
        return NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsServer &&
               NetworkManager.Singleton.SpawnManager != null;
    }

    private IEnumerator SendHeartbeatRoutine()
    {
        while (CanSendHostHeartbeat())
        {
            yield return SendHeartbeatToServer();
            yield return WaitHeartbeatInterval();
        }
    }

    private IEnumerator SendClientHeartbeatRoutine()
    {
        while (CanSendClientHeartbeat())
        {
            yield return TrySendClientHeartbeat();
            yield return WaitHeartbeatInterval();
        }
    }

//──────────────────────────────
// 🔧 Helper Functions
//──────────────────────────────

    private bool CanSendHostHeartbeat()
    {
        return networkManager != null &&
               networkManager.IsHost &&
               !networkManager.ShutdownInProgress;
    }

    private bool CanSendClientHeartbeat()
    {
        return networkManager != null &&
               networkManager.IsClient &&
               !networkManager.ShutdownInProgress;
    }

    private IEnumerator SendHeartbeatToServer()
    {
        using (var www = UnityWebRequest.PostWwwForm($"{SERVER_URL}/{currentLobbyId}/heartbeat", ""))
        {
            www.timeout = 10;
            yield return www.SendWebRequest();
        }
    }

    private IEnumerator TrySendClientHeartbeat()
    {
        if (string.IsNullOrEmpty(currentLobbyId)) yield break;

        string playerId = PlayerPrefs.GetString("PlayerId", "");
        if (string.IsNullOrEmpty(playerId)) yield break;

        var payload = new ClientHeartbeatRequest { UserId = playerId };
        yield return SendJsonPost($"{SERVER_URL}/{currentLobbyId}/client-heartbeat", payload);
    }

    private IEnumerator SendJsonPost(string url, object payload)
    {
        string json = JsonUtility.ToJson(payload);
        using (var www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = 10;
            yield return www.SendWebRequest();
        }
    }

    private WaitForSeconds WaitHeartbeatInterval() => new WaitForSeconds(5f);

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

    private UnityWebRequest CreateGetRequest(string url)
    {
        var req = UnityWebRequest.Get(url);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = 10;
        req.SetRequestHeader("Content-Type", "application/json");
        return req;
    }
}