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
        while (networkManager != null && (networkManager.IsHost || networkManager.IsClient) &&
               !networkManager.ShutdownInProgress)
        {
            if (string.IsNullOrEmpty(currentLobbyId))
            {
                yield return new WaitForSeconds(2f);
                continue;
            }

            using (var www = CreateGetRequest($"{SERVER_URL}/find/{currentLobbyId}"))
            {
                yield return www.SendWebRequest();
                if (!HandleLobbyPollResponse(www))
                {
                    yield return new WaitForSeconds(2f);
                    continue;
                }
            }

            yield return new WaitForSeconds(2f);
        }
    }

    private bool HandleLobbyPollResponse(UnityWebRequest www)
    {
        if (!IsPollResponseValid(www))
            return false;

        RelayLobbyInfo lobby = ParseLobbyInfo(www);
        if (lobby == null)
            return false;

        UpdateLobbyState(lobby);
        return true;
    }

    private RelayLobbyInfo ParseLobbyInfo(UnityWebRequest www)
    {
        try
        {
            return JsonUtility.FromJson<RelayLobbyInfo>(www.downloadHandler.text);
        }
        catch
        {
            Debug.LogWarning("⚠️ Failed to parse lobby JSON response");
            return null;
        }
    }

    private bool IsPollResponseValid(UnityWebRequest www)
    {
        return www.result == UnityWebRequest.Result.Success && this != null && lobbyPanel != null;
    }

    private void UpdateLobbyState(RelayLobbyInfo lobby)
    {
        currentLobbyInfo = lobby;

        string playerId = GetLocalPlayerId();
        var localUser = lobby.users.Find(u => u.userId == playerId);
        if (localUser != null)
            isReady = localUser.isReady;

        UpdatePlayerList(lobby);
    }

    private string GetLocalPlayerId()
    {
        return PlayerPrefs.GetString("PlayerId", "");
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
        using (var www = CreatePostRequest($"{SERVER_URL}/{lobbyId}/leave", user))
        {
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
            while (!op.isDone)
            {
            }
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
            var req = CreatePostRequest($"{SERVER_URL}/{lobbyId}/leave", user);
            req.timeout = 5;
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Leave lobby failed: {e.Message}");
        }
    }

    #endregion
}