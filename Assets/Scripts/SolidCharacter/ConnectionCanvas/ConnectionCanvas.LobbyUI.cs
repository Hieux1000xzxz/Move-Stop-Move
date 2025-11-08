using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public partial class ConnectionCanvas
{
     #region Lobby UI
    private void ShowLobbyUI(RelayLobbyInfo lobby)
    {
        if (joinByIdPanel != null && joinByIdPanel.activeSelf)
            joinByIdPanel.SetActive(false);
        
        currentLobbyInfo = lobby;
        lobbyPanel.SetActive(true);
        lobbyId.text = $"Lobby ID: {lobby.lobbyId}";
        UpdatePlayerList(lobby);
        UpdateReadyButtonStatus();

        if (networkManager.IsHost)
        {
            startGameButton.gameObject.SetActive(true);
            readyButton.gameObject.SetActive(false);
            settingButton.gameObject.SetActive(true);
            isReady = true;
            StartCoroutine(DelayedHostReady());
        }
        else
        {
            startGameButton.gameObject.SetActive(false);
            readyButton.gameObject.SetActive(true);
            settingButton.gameObject.SetActive(false);
            isReady = false;
            string playerId = PlayerPrefs.GetString("PlayerId", "");
            var localUser = lobby.users.Find(u => u.userId == playerId);
            if (localUser != null)
            {
                isReady = localUser.isReady;
            }
        }
    }
    private void UpdatePlayerList(RelayLobbyInfo lobby)
    {
        ClearContainerCache(cachedPlayerItems);

        if (lobby?.users == null) return;

        for (int i = 0; i < lobby.users.Count; i++)
        {
            var user = lobby.users[i];
            var playerItem = GetOrCreateCachedItem(cachedPlayerItems, playerItemPrefab, playerListContainer, i);

            if (playerItem != null)
            {
                playerItem.gameObject.SetActive(true);
                bool canKick = networkManager.IsHost && user.userId != PlayerPrefs.GetString("PlayerId", "");
                Sprite avatar = user.avatarIndex >= 0 && user.avatarIndex < availableAvatars.Length
                    ? availableAvatars[user.avatarIndex] : null;

                playerItem.Setup(user.userName, user.userId, user.clientId, this, canKick, avatar, user.isReady);
            }
        }

        if (networkManager.IsHost)
        {
            bool allReady = AreAllPlayersReady(lobby);
            bool hasEnoughPlayers = lobby.users.Count >= 2;
            startGameButton.interactable = allReady && hasEnoughPlayers;
        }
        else
        {
            startGameButton.interactable = false;
        }
    }

    private bool AreAllPlayersReady(RelayLobbyInfo lobby)
    {
        if (lobby?.users == null || lobby.users.Count == 0) return false;
        foreach (var user in lobby.users)
            if (!user.isReady) return false;
        return true;
    }
    
    private void OnReadyClicked()
    {
        if (!canToggleReady)
        {
            SendNotification("Please wait!!!", 4);
            return;

        }

        isReady = !isReady;
        UpdateReadyButtonStatus();
        StartCoroutine(UpdateReadyStatus(isReady));
        StartCoroutine(ReadyCooldown());
    }

    private IEnumerator ReadyCooldown()
    {
        canToggleReady = false;
        yield return new WaitForSeconds(2f);
        canToggleReady = true;
    }

    private void UpdateReadyButtonStatus()
    {
        readyButton.image.sprite = isReady ? unReadySprite : readySprite;
    }

    private IEnumerator UpdateReadyStatus(bool ready)
    {
        string playerId = GetOrCreatePlayerId();
        if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(currentLobbyId))
            yield break;

        var payload = new UpdateReadyRequest { UserId = playerId, IsReady = ready };
        using (var www = CreatePostRequest($"{SERVER_URL}/{currentLobbyId}/ready", payload))
        {
            yield return www.SendWebRequest();
        }
    }
    
    private IEnumerator DelayedHostReady()
    {
        yield return new WaitForSeconds(0.3f);
        StartCoroutine(UpdateReadyStatus(true));
    }
    #endregion
}
