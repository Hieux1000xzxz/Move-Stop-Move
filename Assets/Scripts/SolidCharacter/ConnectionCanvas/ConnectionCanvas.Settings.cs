using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public partial class ConnectionCanvas
{
    #region Settings Panel

    public void ShowSettingPanel()
    {
        if (settingPanel != null)
        {
            settingPanel.SetActive(true);
            if (roomNameInputField != null && currentLobbyInfo != null)
                roomNameInputField.text = currentLobbyInfo.lobbyName;
        }
    }

    public void HideSettingPanel()
    {
        if (settingPanel != null)
            settingPanel.SetActive(false);
    }

    private void OnConfirmRoomNameChange()
    {
        if (roomNameInputField == null) return;

        string newName = roomNameInputField.text.Trim();

        if (!IsValidString(newName, "Room name"))
            return;

        if (currentLobbyInfo == null) currentLobbyInfo = new RelayLobbyInfo { lobbyId = currentLobbyId };
        currentLobbyInfo.lobbyName = newName;

        StartCoroutine(UpdateRoomNameRoutine(currentLobbyInfo.lobbyId, newName));
        HideSettingPanel();
    }

    private IEnumerator UpdateRoomNameRoutine(string lobbyId, string newName)
    {
        var reqObj = new UpdateLobbyNameRequest
        {
            lobbyId = lobbyId,
            lobbyName = newName,
            requestingUserId = GetOrCreatePlayerId()
        };

        string url = $"{SERVER_URL}/{lobbyId}/rename";

        using (var www = CreatePostRequest(url, reqObj))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                SendNotification("Room name updated successfully", 4);
                StartCoroutine(RefreshCurrentLobbyInfo());
            }
            else
            {
                SendNotification("Failed to update room name. Please try again.", 1);
            }
        }
    }

    private IEnumerator RefreshCurrentLobbyInfo()
    {
        if (string.IsNullOrEmpty(currentLobbyId)) yield break;

        using (var www = UnityWebRequest.Get($"{SERVER_URL}/find/{currentLobbyId}"))
        {
            www.timeout = 10;
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                RelayLobbyInfo lobby = JsonUtility.FromJson<RelayLobbyInfo>(www.downloadHandler.text);
                if (lobby != null)
                {
                    currentLobbyInfo = lobby;
                    UpdatePlayerList(lobby);
                }
            }
        }
    }

    #endregion
}