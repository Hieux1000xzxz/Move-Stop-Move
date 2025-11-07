using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
public partial class ConnectionCanvas
{
     #region  Initialization
    private void InitializeButtons()
    {
        singleButton.onClick.AddListener(OnSinglePlayerClicked);
        onlineButton.onClick.AddListener(OnOnlineClicked);
        backToMenuButton.onClick.AddListener(OnBackToMenu);

        backButton.onClick.AddListener(OnBackToModePanel);
        startHostButton.onClick.AddListener(OnStartHostClicked);
        joinByIdButton.onClick.AddListener(ShowJoinByIdPanel);
        editProfileButton.onClick.AddListener(ShowEditProfilePanel);

        startGameButton.onClick.AddListener(OnStartGameClicked);
        readyButton.onClick.AddListener(OnReadyClicked);
        settingButton.onClick.AddListener(ShowSettingPanel);
        exitButton.onClick.AddListener(OnExitClicked);
        editProfileInLobbyButton.onClick.AddListener(ShowEditProfilePanel);

        confirmJoinButton.onClick.AddListener(OnConfirmJoinById);
        cancelJoinButton.onClick.AddListener(CloseJoinByIdPanel);

        confirmRoomNameButton.onClick.AddListener(OnConfirmRoomNameChange);
        closeSettingButton.onClick.AddListener(HideSettingPanel);

        confirmPlayerInfoButton.onClick.AddListener(OnConfirmPlayerInfo);
        cancelPlayerInfoButton.onClick.AddListener(OnCancelPlayerInfo);
    }
    private void InitializeNetworkCallbacks()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            networkManager.OnServerStarted += OnServerStarted;
        }
    }
    private void SetInitialUIState()
    {
        modePanel.SetActive(true);
        lobbyPanel.SetActive(false);
        mainPanel.SetActive(false);
        if (joinByIdPanel != null) joinByIdPanel.SetActive(false);
        if (playerInfoPanel != null) playerInfoPanel.SetActive(false);
        if (settingPanel != null) settingPanel.SetActive(false);
    }
    private void InitializeInputValidation()
    {
        if (playerNameInputField != null && confirmPlayerInfoButton != null)
        {
            confirmPlayerInfoButton.interactable = false;
            playerNameInputField.onValueChanged.AddListener(value =>
            {
                confirmPlayerInfoButton.interactable = !string.IsNullOrWhiteSpace(value);
            });
        }

        if (lobbyIdInputField != null && confirmJoinButton != null)
        {
            confirmJoinButton.interactable = false;
            lobbyIdInputField.onValueChanged.AddListener(value =>
            {
                confirmJoinButton.interactable = !string.IsNullOrWhiteSpace(value);
            });
        }

        if (roomNameInputField != null && confirmRoomNameButton != null)
        {
            confirmRoomNameButton.interactable = false;
            roomNameInputField.onValueChanged.AddListener(value =>
            {
                confirmRoomNameButton.interactable = !string.IsNullOrWhiteSpace(value);
            });
        }
    }
    private async Task InitializeUnityServices()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            isUnityServicesInitialized = true;
            Debug.Log($"Unity Services initialized. Player ID: {AuthenticationService.Instance.PlayerId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize Unity Services: {e.Message}");
            isUnityServicesInitialized = false;
        }
    }
    private void InitializeAvatarSelection()
    {
        if (avatarSelectionContainer == null || avatarItemPrefab == null)
        {
            Debug.LogError("Avatar components not assigned!");
            return;
        }

        if (availableAvatars == null || availableAvatars.Length == 0)
        {
            Debug.LogWarning("No available avatars assigned.");
            return;
        }

        ClearContainer(avatarSelectionContainer);
        cachedAvatarItems.Clear();

        for (int i = 0; i < availableAvatars.Length; i++)
        {
            var avatarItem = Instantiate(avatarItemPrefab, avatarSelectionContainer);
            if (avatarItem != null)
            {
                avatarItem.Initialize(i, availableAvatars[i], SelectAvatar);
                cachedAvatarItems.Add(avatarItem);
            }
        }

        SelectAvatar(selectedAvatarIndex);
    }
    private void SelectAvatar(int index)
    {
        selectedAvatarIndex = index;
        for (int i = 0; i < cachedAvatarItems.Count; i++)
        {
            if (cachedAvatarItems[i] != null)
                cachedAvatarItems[i].SetHighlight(i == index);
        }
    }
    private void LoadPlayerPrefs()
    {
        localUserName = PlayerPrefs.GetString("PlayerName", DEFAULT_PLAYER_NAME);
        selectedAvatarIndex = PlayerPrefs.GetInt("PlayerAvatar", 0);

        string playerId = PlayerPrefs.GetString("PlayerId", "");
        if (string.IsNullOrEmpty(playerId))
        {
            playerId = System.Guid.NewGuid().ToString();
            PlayerPrefs.SetString("PlayerId", playerId);
            PlayerPrefs.Save();
        }

        SelectAvatar(selectedAvatarIndex);
    }
    private void SavePlayerPrefs(string playerName)
    {
        string playerId = PlayerPrefs.GetString("PlayerId", "");
        if (string.IsNullOrEmpty(playerId))
        {
            playerId = System.Guid.NewGuid().ToString();
            PlayerPrefs.SetString("PlayerId", playerId);
        }

        PlayerPrefs.SetString("PlayerName", playerName);
        PlayerPrefs.SetInt("PlayerAvatar", selectedAvatarIndex);
        PlayerPrefs.Save();
    }

    #endregion
}
