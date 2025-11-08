using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public partial class ConnectionCanvas : BaseCanvas
{
    #region Player Profile (Single Responsibility)

    // 🧩 1. Lấy hoặc tạo PlayerId (chung cho Single và Multi)
    private string GetOrCreatePlayerId()
    {
        string id = PlayerPrefs.GetString("PlayerId", "");
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString();
            PlayerPrefs.SetString("PlayerId", id);
            PlayerPrefs.Save();
            Debug.Log($"🆔 Created new PlayerId: {id}");
        }
        return id;
    }

    // 🧩 2. Tải tên người chơi
    private string LoadPlayerName()
    {
        string name = PlayerPrefs.GetString("PlayerName", DEFAULT_PLAYER_NAME);
        Debug.Log($"👤 Loaded PlayerName: {name}");
        return name;
    }

    // 🧩 3. Lưu tên người chơi
    private void SavePlayerName(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            newName = DEFAULT_PLAYER_NAME;

        PlayerPrefs.SetString("PlayerName", newName);
        PlayerPrefs.Save();
        localUserName = newName;
        Debug.Log($"💾 Saved PlayerName: {newName}");
    }

    // 🧩 4. Tải avatar đã chọn
    private int LoadAvatarIndex()
    {
        int index = PlayerPrefs.GetInt("PlayerAvatar", 0);
        Debug.Log($"🎨 Loaded Avatar Index: {index}");
        return index;
    }

    // 🧩 5. Lưu avatar đã chọn
    private void SaveAvatarIndex(int index)
    {
        PlayerPrefs.SetInt("PlayerAvatar", index);
        PlayerPrefs.Save();
        Debug.Log($"💾 Saved Avatar Index: {index}");
    }

    // 🧩 6. Khởi tạo thông tin người chơi khi vào game
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

        // Nếu đang trong lobby online, gửi thông tin cập nhật lên server
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
