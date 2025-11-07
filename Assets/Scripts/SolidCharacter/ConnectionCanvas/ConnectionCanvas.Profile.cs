using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

public partial class ConnectionCanvas
{
       #region PlayerInfor & Avatar
    private void ShowEditProfilePanel()
    {
        if (playerInfoPanel != null)
        {
            playerInfoPanel.SetActive(true);
            SelectAvatar(PlayerPrefs.GetInt("PlayerAvatar", selectedAvatarIndex));
            if (playerNameInputField != null)
            {
                playerNameInputField.text = localUserName;
            }
            if (playerInfoTitle != null)
            {
                playerInfoTitle.text = "Edit Profile";
            }
        }
    }

    private void HidePlayerInfoPanel()
    {
        if (playerInfoPanel != null)
        {
            playerInfoPanel.SetActive(false);
        }
    }

    private void OnConfirmPlayerInfo()
    {
        string newName = playerNameInputField != null ? playerNameInputField.text.Trim() : "";
        if (!IsValidString(newName, "Player name"))
            return;
        SendNotification("Profile updated successfully", 4);

        localUserName = newName;
        SavePlayerPrefs(localUserName);
        HidePlayerInfoPanel();

        if (!string.IsNullOrEmpty(currentLobbyId) && networkManager.IsClient)
        {
            StartCoroutine(UpdatePlayerInfoInLobby());
        }
    }

    private IEnumerator UpdatePlayerInfoInLobby()
    {
        string playerId = PlayerPrefs.GetString("PlayerId", "");
        if (string.IsNullOrEmpty(playerId)) yield break;

        var user = new UserInfo
        {
            userId = playerId,
            userName = localUserName,
            avatarIndex = selectedAvatarIndex
        };

        using (var www = CreatePostRequest($"{SERVER_URL}/{currentLobbyId}/updateplayer", user))
        {
            yield return www.SendWebRequest();
        }
    }

    private void OnCancelPlayerInfo()
    {
        HidePlayerInfoPanel();
    }
    
    #endregion
}
