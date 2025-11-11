using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public partial class ConnectionCanvas : BaseCanvas
{
    #region Player Profile (Single Responsibility)

    private string GetOrCreatePlayerId()
    {
        string id = PlayerPrefs.GetString("PlayerId", "");
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString();
            PlayerPrefs.SetString("PlayerId", id);
            PlayerPrefs.Save();
        }

        return id;
    }

    private string LoadPlayerName()
    {
        string name = PlayerPrefs.GetString("PlayerName", DEFAULT_PLAYER_NAME);
        return name;
    }

    private void SavePlayerName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            newName = DEFAULT_PLAYER_NAME;

        PlayerPrefs.SetString("PlayerName", newName);
        PlayerPrefs.Save();
        localUserName = newName;
    }

    private int LoadAvatarIndex()
    {
        int index = PlayerPrefs.GetInt("PlayerAvatar", 0);
        return index;
    }

    private void SaveAvatarIndex(int index)
    {
        PlayerPrefs.SetInt("PlayerAvatar", index);
        PlayerPrefs.Save();
    }

    private void InitializePlayerProfile()
    {
        localUserName = LoadPlayerName();
        selectedAvatarIndex = LoadAvatarIndex();
        GetOrCreatePlayerId();
        SelectAvatar(selectedAvatarIndex);
    }

    #endregion

    #region Player Info UI

    private void ShowEditProfilePanel()
    {
        if (playerInfoPanel != null)
        {
            playerInfoPanel.SetActive(true);
            SelectAvatar(PlayerPrefs.GetInt("PlayerAvatar", selectedAvatarIndex));

            if (playerNameInputField != null)
                playerNameInputField.text = localUserName;

            if (playerInfoTitle != null)
                playerInfoTitle.text = "Edit Profile";
        }
    }

    private void HidePlayerInfoPanel()
    {
        if (playerInfoPanel != null)
            playerInfoPanel.SetActive(false);
    }

    private void OnConfirmPlayerInfo()
    {
        string newName = playerNameInputField != null ? playerNameInputField.text.Trim() : "";

        if (!IsValidString(newName, "Player name"))
            return;

        SendNotification("Profile updated successfully", 4);

        localUserName = newName;
        SavePlayerName(localUserName);
        SaveAvatarIndex(selectedAvatarIndex);
        HidePlayerInfoPanel();

        if (!string.IsNullOrEmpty(currentLobbyId) && networkManager.IsClient)
            StartCoroutine(UpdatePlayerInfoInLobby());
    }

    private IEnumerator UpdatePlayerInfoInLobby()
    {
        string playerId = GetOrCreatePlayerId();
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