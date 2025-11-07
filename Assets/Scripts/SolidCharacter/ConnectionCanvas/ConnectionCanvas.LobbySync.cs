using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
public partial class ConnectionCanvas
{
       #region Lobby Server Sync
    private IEnumerator PollLobbyInfo()
    {
        while (networkManager != null && (networkManager.IsHost || networkManager.IsClient) && !networkManager.ShutdownInProgress)
        {
            if (!string.IsNullOrEmpty(currentLobbyId))
            {
                using (var www = UnityWebRequest.Get($"{SERVER_URL}/find/{currentLobbyId}"))
                {
                    www.timeout = 10;
                    yield return www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success && this != null && lobbyPanel != null)
                    {
                        RelayLobbyInfo lobby = JsonUtility.FromJson<RelayLobbyInfo>(www.downloadHandler.text);
                        if (lobby != null)
                        {
                            currentLobbyInfo = lobby;

                            string playerId = PlayerPrefs.GetString("PlayerId", "");
                            var localUser = lobby.users.Find(u => u.userId == playerId);
                            if (localUser != null)
                            {
                                isReady = localUser.isReady;
                            }

                            UpdatePlayerList(lobby);
                        }
                    }
                }
            }
            yield return new WaitForSeconds(2f);
        }
    }
    private IEnumerator DeleteLobbyRoutine(string lobbyId)
    {
        using (var www = UnityWebRequest.Delete($"{SERVER_URL}/unregister/{lobbyId}"))
        {
            www.timeout = 10;
            yield return www.SendWebRequest();
        }
    }
    private IEnumerator LeaveLobbyRoutine(string lobbyId, UserInfo user)
    {
        string json = JsonUtility.ToJson(user);

        using (var www = new UnityWebRequest($"{SERVER_URL}/{lobbyId}/leave", "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = 10;

            yield return www.SendWebRequest();
        }
    }    
    private void SendDeleteLobbySync(string lobbyId)
    {
        try
        {
            var req = UnityWebRequest.Delete($"{SERVER_URL}/unregister/{lobbyId}");
            req.timeout = 5;
            var op = req.SendWebRequest();
            while (!op.isDone) { }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Delete lobby failed: {e.Message}");
        }
    }
    private void SendLeaveLobbySync(string lobbyId, UserInfo user)
    {
        try
        {
            string json = JsonUtility.ToJson(user);
            var req = new UnityWebRequest($"{SERVER_URL}/{lobbyId}/leave", "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 5;

            var op = req.SendWebRequest();
            while (!op.isDone) { }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Leave lobby failed: {e.Message}");
        }
    }
    #endregion

}
