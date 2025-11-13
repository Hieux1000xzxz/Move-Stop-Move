using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine.UI; // ✅ thêm dòng này
using TMPro;
using UnityEngine;

public partial class ConnectionCanvas
{
    #region Initialization

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
        }
    }

    private void InitializeInputValidation()
    {
        SetupInputValidation(playerNameInputField, confirmPlayerInfoButton);
        SetupInputValidation(lobbyIdInputField, confirmJoinButton);
        SetupInputValidation(roomNameInputField, confirmRoomNameButton);
    }

    private void SetupInputValidation(TMP_InputField inputField, Button confirmButton)
    {
        if (inputField == null || confirmButton == null)
            return;

        confirmButton.interactable = false;
        inputField.onValueChanged.AddListener(value =>
        {
            confirmButton.interactable = !string.IsNullOrWhiteSpace(value);
        });
    }

    private async Task InitializeUnityServices()
    {
        try
        {
            await EnsureUnityServicesInitialized();
            await EnsureUserSignedIn();

            isUnityServicesInitialized = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Unity Services init failed: {e.Message}");
            isUnityServicesInitialized = false;
        }
    }

    private async Task EnsureUnityServicesInitialized()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();
    }

    private async Task EnsureUserSignedIn()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    #endregion


    #region 🧩 Avatar Selection

    private void InitializeAvatarSelection()
    {
        if (!IsAvatarSetupValid())
            return;

        ClearContainer(avatarSelectionContainer);
        cachedAvatarItems.Clear();

        for (int i = 0; i < availableAvatars.Length; i++)
            CreateAvatarItem(i);

        SelectAvatar(selectedAvatarIndex);
    }

    private bool IsAvatarSetupValid()
    {
        return avatarSelectionContainer != null &&
               avatarItemPrefab != null &&
               availableAvatars != null &&
               availableAvatars.Length > 0;
    }

    private void CreateAvatarItem(int index)
    {
        var avatarItem = Instantiate(avatarItemPrefab, avatarSelectionContainer);
        if (avatarItem == null) return;

        avatarItem.Initialize(index, availableAvatars[index], SelectAvatar);
        cachedAvatarItems.Add(avatarItem);
    }

    #endregion

    private void SelectAvatar(int index)
    {
        selectedAvatarIndex = index;
        for (int i = 0; i < cachedAvatarItems.Count; i++)
        {
            if (cachedAvatarItems[i] != null)
                cachedAvatarItems[i].SetHighlight(i == index);
        }
    }

    private void SetUIState(bool isInMainMenu)
    {
        modePanel.SetActive(isInMainMenu);
        mainPanel.SetActive(!isInMainMenu);
        lobbyPanel.SetActive(false);
        joinByIdPanel?.SetActive(false);
        playerInfoPanel?.SetActive(false);
        settingPanel?.SetActive(false);
    }
}