using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ConnectionCanvas : BaseCanvas
{
    [Header("UI Panels")]
    [SerializeField] private GameObject modePanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject playerInfoPanel;
    [SerializeField] private GamePlayCanvas gameplayCanvas;

    [Header("Mode Panel")]
    [SerializeField] private Button singleButton;
    [SerializeField] private Button onlineButton;
    [SerializeField] private Button backToMenuButton;

    [Header("Main Panel")]
    [SerializeField] private Button startHostButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button joinByIdButton;
    [SerializeField] private Button editProfileButton;
    [SerializeField] private Transform lobbyListContainer;
    [SerializeField] private LobbyItem lobbyItemPrefab;

    [Header("Lobby Panel")]
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private TextMeshProUGUI lobbyId;
    [SerializeField] private PlayerItem playerItemPrefab;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button settingButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button editProfileInLobbyButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private Sprite readySprite;
    [SerializeField] private Sprite unReadySprite;

    [Header("Join by ID Panel")]
    [SerializeField] private GameObject joinByIdPanel;
    [SerializeField] private TMP_InputField lobbyIdInputField;
    [SerializeField] private Button confirmJoinButton;
    [SerializeField] private Button cancelJoinButton;

    [Header("Player Info Panel")]
    [SerializeField] private TMP_InputField playerNameInputField;
    [SerializeField] private Transform avatarSelectionContainer;
    [SerializeField] private AvatarItem avatarItemPrefab;
    [SerializeField] private Button confirmPlayerInfoButton;
    [SerializeField] private Button cancelPlayerInfoButton;
    [SerializeField] private TextMeshProUGUI playerInfoTitle;

    [Header("Setting Panel")]
    [SerializeField] private GameObject settingPanel;
    [SerializeField] private TMP_InputField roomNameInputField;
    [SerializeField] private Button confirmRoomNameButton;
    [SerializeField] private Button closeSettingButton;

    [Header("Network")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport transport;

    [SerializeField] private GameObject playerPreview;
    [SerializeField] private CinemachineCamera mainCamera;
    [SerializeField] private Sprite[] availableAvatars;

    [Header("Cache Lists")]
    [SerializeField] private List<AvatarItem> cachedAvatarItems = new List<AvatarItem>();
    [SerializeField] private List<PlayerItem> cachedPlayerItems = new List<PlayerItem>();
    [SerializeField] private List<LobbyItem> cachedLobbyItems = new List<LobbyItem>();

    [Header("Status Flags")]
    private bool isCreatingRoom = false;
    private bool isJoiningRoom = false;

    private const string SERVER_URL = "https://mini-server-v6.onrender.com/api/lobby";
    private const string DEFAULT_LOBBY_NAME = "Lobby";
    private const string DEFAULT_PLAYER_NAME = "Player";

    private int selectedAvatarIndex = 0;
    private string currentLobbyId = string.Empty;
    private string localUserName = DEFAULT_PLAYER_NAME;
    private string currentRelayJoinCode = string.Empty;

    private Coroutine heartbeatRoutine;
    private Coroutine pollLobbyRoutine;
    private Coroutine autoRefreshLobbyRoutine;
    private RelayLobbyInfo pendingLobbyJoin;
    public bool isSinglePlayerMode = false;
    private bool isReady = false;
    private RelayLobbyInfo currentLobbyInfo;
    private bool isUnityServicesInitialized = false;
    private bool canToggleReady = true;
    private Coroutine clientHeartbeatRoutine;
    private bool isIntentionalDisconnect = false;

    private async void Start()
    {
        InitializeButtons();
        InitializeNetworkCallbacks();
        SetInitialUIState();
        LoadPlayerPrefs();

        await InitializeUnityServices();
        UpdateReadyButtonStatus();
        RefreshLobbyList();
        InitializeAvatarSelection();
        InitializeInputValidation();
        autoRefreshLobbyRoutine = StartCoroutine(AutoRefreshLobbyList());
       
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

    private T GetOrCreateCachedItem<T>(List<T> itemCache, T prefab, Transform parent, int index) where T : Component
    {
        if (index < itemCache.Count && itemCache[index] != null)
        {
            itemCache[index].gameObject.SetActive(true);
            return itemCache[index];
        }
        else
        {
            var newItem = Instantiate(prefab, parent);
            if (index >= itemCache.Count)
            {
                itemCache.Add(newItem);
            }
            else
            {
                itemCache[index] = newItem;
            }

            return newItem;
        }
    }

    private void ClearContainerCache<T>(List<T> itemCache) where T : Component
    {
        foreach (var item in itemCache)
        {
            if (item != null && item.gameObject != null)
                item.gameObject.SetActive(false);
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

    public void RefreshLobbyList()
    {
        if (this != null)
            StartCoroutine(RefreshLobbyListRoutine());
    }

    private IEnumerator RefreshLobbyListRoutine()
    {
        using (var www = UnityWebRequest.Get($"{SERVER_URL}/list"))
        {
            www.timeout = 10;
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string json = www.downloadHandler.text;
                RelayLobbyInfo[] lobbies = JsonHelper.FromJson<RelayLobbyInfo>(json);
                ClearContainerCache(cachedLobbyItems);

                for (int i = 0; i < lobbies.Length; i++)
                {
                    var lobbyItem = GetOrCreateCachedItem(cachedLobbyItems, lobbyItemPrefab, lobbyListContainer, i);
                    if (lobbyItem != null)
                        lobbyItem.Setup(lobbies[i], this);
                }
            }
        }
    }

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

    private void OnSinglePlayerClicked()
    {
        isSinglePlayerMode = true;
        localUserName = PlayerPrefs.GetString("PlayerName", DEFAULT_PLAYER_NAME);
        currentLobbyId = "SINGLE";

        if (networkManager != null)
        {
            transport.SetConnectionData("127.0.0.1", 7777);
            networkManager.StartHost();
        }

        StartCoroutine(StartSinglePlayerGame());
        modePanel.SetActive(false);
    }

    private IEnumerator StartSinglePlayerGame()
    {
        yield return null;
        GameManager.Instance.StartGame();
        GameManager.Instance.StartPowerupSpawning();

        if (gameplayCanvas != null)
            gameplayCanvas.Init(this, "SINGLE", localUserName);
    }

    private async void OnOnlineClicked()
    {
        isSinglePlayerMode = false;
        SendNotification("Checking connection...", 2);
        if (!isUnityServicesInitialized)
            await InitializeUnityServices();

        StartCoroutine(CheckNetworkAndShowMainPanel());
    }

    private IEnumerator CheckNetworkAndShowMainPanel()
    {
        bool serverAvailable = false;
        yield return StartCoroutine(CheckServerAvailabilityCoroutine((result) => serverAvailable = result));

        if (serverAvailable && isUnityServicesInitialized)
        {
            UIManager.Instance?.CloseNotification();
            modePanel.SetActive(false);
            mainPanel.SetActive(true);
            EnableMainPanelButton();
        }
        else
        {
            string message = !isUnityServicesInitialized
                ? "Unable to connect to online services. Please check your internet connection and try again."
                : "Connection failed. Please check your internet connection.";
            SendNotification(message, 1);
        }
    }

    private void OnReadyClicked()
    {
        if (!canToggleReady)
        {
            UIManager.Instance.SendNotification("Please wait!!!", 4);
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
        string playerId = PlayerPrefs.GetString("PlayerId", "");
        if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(currentLobbyId))
            yield break;

        var payload = new UpdateReadyRequest { UserId = playerId, IsReady = ready };
        string json = JsonUtility.ToJson(payload);

        using (var www = new UnityWebRequest($"{SERVER_URL}/{currentLobbyId}/ready", "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = 10;

            yield return www.SendWebRequest();
        }
    }

    private void OnBackToModePanel()
    {
        if (isCreatingRoom || isJoiningRoom) return;

        mainPanel.SetActive(false);
        modePanel.SetActive(true);
    }

    private void OnStartHostClicked()
    {
        if (isCreatingRoom || isJoiningRoom) return;
        DisableMainPanelButton();
        ResetNetworkManager();
        StartHost();
    }

    private async void StartHost()
    {
        if (!isUnityServicesInitialized)
        {
            SendNotification("Online services are not ready. Please try again.", 1);
            return;
        }
        isCreatingRoom = true;
        SendNotification("Creating lobby...", 2);

        string hostUserId = PlayerPrefs.GetString("PlayerId", "");
        if (string.IsNullOrEmpty(hostUserId))
        {
            hostUserId = System.Guid.NewGuid().ToString();
            PlayerPrefs.SetString("PlayerId", hostUserId);
            PlayerPrefs.Save();
        }

        string joinCode = await CreateRelayAllocation(6);
        if (string.IsNullOrEmpty(joinCode))
        {
            SendNotification("Failed to create lobby. Please try again.", 1);
            EnableMainPanelButton();
            isCreatingRoom = false;
            return;
        }

        if (!networkManager.StartHost())
        {
            EnableMainPanelButton();
            isCreatingRoom = false;
            SendNotification("Failed to start host. Please try again.", 1);
            return;
        }

        string lobbyName = GenerateRandomLobbyName();
        StartCoroutine(StartHostRoutineCoroutine(localUserName, lobbyName, joinCode, hostUserId));
    }

    private string GenerateRandomLobbyName()
    {
        System.Random random = new System.Random();
        int randomNumber = random.Next(1000, 10000);
        return $"Lobby {randomNumber}";
    }
    private IEnumerator StartHostRoutineCoroutine(string hostName, string lobbyName, string joinCode, string hostUserId)
    {
        var request = new RelayLobbyRegistrationRequest
        {
            lobbyName = lobbyName,
            relayJoinCode = joinCode,
            maxPlayers = 6,
            hostName = hostName,
            AvatarIndex = PlayerPrefs.GetInt("PlayerAvatar", 0),
            HostUserId = hostUserId
        };

        bool serverAvailable = false;
        yield return StartCoroutine(CheckServerAvailabilityCoroutine((result) => serverAvailable = result));

        if (serverAvailable)
        {
            yield return StartCoroutine(RegisterRelayLobbyOnServer(request));
        }
    }

    private IEnumerator CheckServerAvailabilityCoroutine(System.Action<bool> callback)
    {
        using (var www = UnityWebRequest.Get($"{SERVER_URL}/ping"))
        {
            www.timeout = 10;
            yield return www.SendWebRequest();
            callback(www.result == UnityWebRequest.Result.Success);
        }
    }

    private IEnumerator RegisterRelayLobbyOnServer(RelayLobbyRegistrationRequest request)
    {
        string json = JsonUtility.ToJson(request);

        using (var www = new UnityWebRequest($"{SERVER_URL}/register", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = 10;

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var lobby = JsonUtility.FromJson<RelayLobbyInfo>(www.downloadHandler.text);
                currentLobbyId = lobby.lobbyId;

                SendNotification("Lobby created successfully!", 4);
                ShowLobbyUI(lobby);
                mainPanel.SetActive(false);
                heartbeatRoutine = StartCoroutine(SendHeartbeatRoutine());
                pollLobbyRoutine = StartCoroutine(PollLobbyInfo());
            }
            else
            {
                SendNotification("Failed to create lobby. Please try again.", 1);
                Debug.LogWarning("Could not reach lobby server");

                if (networkManager.IsHost)
                {
                    networkManager.Shutdown();
                }
            }
            isCreatingRoom = false;
        }
    }

    public void ShowJoinByIdPanel()
    {
        if (isCreatingRoom || isJoiningRoom) return;

        if (joinByIdPanel != null)
        {
            joinByIdPanel.SetActive(true);
            lobbyIdInputField.text = string.Empty;
            DisableMainPanelButton();

        }
    }

    public void DisableMainPanelButton()
    {
        startHostButton.interactable = false;
        joinByIdButton.interactable = false;
        backButton.interactable = false;
    }

    public void EnableMainPanelButton()
    {
        startHostButton.interactable = true;
        joinByIdButton.interactable = true;
        backButton.interactable = true;
    }

    private void CloseJoinByIdPanel()
    {
        if (joinByIdPanel != null)
        {
            joinByIdPanel.SetActive(false);
            EnableMainPanelButton();
        }
    }

    private void OnConfirmJoinById()
    {
        string lobbyId = lobbyIdInputField.text.Trim();
        if (!Regex.IsMatch(lobbyId, @"^[a-zA-Z0-9 ]+$"))
        {
            SendNotification("Please use only letters and numbers. Special characters are not allowed.", 1);
            return;
        }
        if (string.IsNullOrEmpty(lobbyId))
        {
            SendNotification("Please enter a valid Lobby ID", 1);
            return;
        }

        CloseJoinByIdPanel();
        StartCoroutine(JoinLobbyRoutine(lobbyId, localUserName));
    }

    public void JoinLobbyDirect(RelayLobbyInfo lobby)
    {
        if (isJoiningRoom) return;
        if (lobby == null) return;
        
        isJoiningRoom = true; // ✅ Đặt cờ
        DisableAllJoinButtons();
            
        ResetNetworkManager();
        StartCoroutine(JoinLobbyRoutine(lobby.lobbyId, localUserName));
    }

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
        if (!Regex.IsMatch(newName, @"^[a-zA-Z0-9 ]+$"))
        {
            SendNotification("Please use only letters and numbers. Special characters are not allowed.", 1);
            return;
        }
        if (string.IsNullOrEmpty(newName))
        {
            SendNotification("Player name cannot be empty", 1);
            return;
        }
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

        string json = JsonUtility.ToJson(user);

        using (var www = new UnityWebRequest($"{SERVER_URL}/{currentLobbyId}/updateplayer", "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = 10;

            yield return www.SendWebRequest();
        }
    }

    private void OnCancelPlayerInfo()
    {
        HidePlayerInfoPanel();
    }

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

    private void OnStartGameClicked()
    {
        if (networkManager.IsHost)
        {
            StartCoroutine(StartGameSequence());
        }
    }

    private IEnumerator StartGameSequence()
    {
        startGameButton.interactable = false;

        yield return StartCoroutine(StartGameOnServerRoutine());

        GameManager.Instance.StartGame();
        GameManager.Instance.StartGameClientRpc();
        GameManager.Instance.StartPowerupSpawning();

        if (gameplayCanvas != null)
        {
            gameplayCanvas.Init(this, currentLobbyId, localUserName);
        }
    }

    private void OnExitClicked()
    {
        HandleExitLogic();
    }

    public void HandleExitLogic()
    {
        if (isSinglePlayerMode)
        {
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }
            GameManager.Instance.StopPowerupSpawning();
            ResetState();
            ResetNetworkManager();
            modePanel.SetActive(true);
            return;
        }

        isIntentionalDisconnect = true;
        isCreatingRoom = false;
        isJoiningRoom = false;
        if (clientHeartbeatRoutine != null)
        {
            StopCoroutine(clientHeartbeatRoutine);
            clientHeartbeatRoutine = null;
        }

        if (networkManager == null) return;

        bool wasHost = networkManager.IsHost;
        bool wasClient = networkManager.IsClient;
        string playerId = PlayerPrefs.GetString("PlayerId", "");
        string lobbyId = currentLobbyId;

        if (networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        ResetNetworkManager();

        if (wasHost)
        {
            if (heartbeatRoutine != null) StopCoroutine(heartbeatRoutine);
            if (!string.IsNullOrEmpty(lobbyId))
            {
                StartCoroutine(DeleteLobbyRoutine(lobbyId));
            }
        }
        else if (wasClient)
        {
            if (!string.IsNullOrEmpty(lobbyId) && !string.IsNullOrEmpty(playerId))
            {
                var user = new UserInfo { userId = playerId, userName = localUserName };
                StartCoroutine(LeaveLobbyRoutine(lobbyId, user));
            }
        }

        if (mainCamera != null && playerPreview != null)
        {
            mainCamera.Follow = playerPreview.transform;
            mainCamera.LookAt = playerPreview.transform;
        }

        GameManager.Instance.StopPowerupSpawning();
        ResetState();
        ResetUIState();
        RefreshLobbyList();

        isIntentionalDisconnect = false;
    }

    private void ResetState()
    {
        currentLobbyId = string.Empty;
        currentRelayJoinCode = string.Empty;
        isSinglePlayerMode = false;
        currentLobbyInfo = null;
        isReady = false;
    }

    private void ResetUIState()
    {
        lobbyPanel.SetActive(false);
        if (joinByIdPanel != null) joinByIdPanel.SetActive(false);
        if (playerInfoPanel != null) playerInfoPanel.SetActive(false);
        if (settingPanel != null) settingPanel.SetActive(false);
        mainPanel.SetActive(true);
        EnableMainPanelButton();
        modePanel.SetActive(false);
        isCreatingRoom = false;
        isJoiningRoom = false;
    }

    private void ShowLobbyUI(RelayLobbyInfo lobby)
    {
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

    private IEnumerator DelayedHostReady()
    {
        yield return new WaitForSeconds(0.3f);
        StartCoroutine(UpdateReadyStatus(true));
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

    private IEnumerator JoinLobbyRoutine(string lobbyId, string playerName)
    {
        isJoiningRoom = true;
        try
        {
            using (var checkWww = UnityWebRequest.Get($"{SERVER_URL}/find/{lobbyId}"))
            {
                checkWww.timeout = 10;
                yield return checkWww.SendWebRequest();

                if (checkWww.result != UnityWebRequest.Result.Success)
                {
                    SendNotification("Failed to join lobby. Please try again.", 1);
                    HandleExitLogic();
                    yield break;
                }

                RelayLobbyInfo lobbyCheck = JsonUtility.FromJson<RelayLobbyInfo>(checkWww.downloadHandler.text);
                if (lobbyCheck == null || lobbyCheck.isGameStarted)
                {
                    SendNotification("Lobby not found or game already started.", 1);
                    yield break;
                }
            }

            string playerId = PlayerPrefs.GetString("PlayerId", "");
            if (string.IsNullOrEmpty(playerId))
            {
                playerId = System.Guid.NewGuid().ToString();
                PlayerPrefs.SetString("PlayerId", playerId);
            }

            var user = new UserInfo
            {
                userId = playerId,
                userName = playerName,
                avatarIndex = selectedAvatarIndex
            };
            string json = JsonUtility.ToJson(user);

            using (var www = new UnityWebRequest($"{SERVER_URL}/{lobbyId}/join", "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = 10;

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    RelayLobbyInfo lobby = JsonUtility.FromJson<RelayLobbyInfo>(www.downloadHandler.text);
                    currentLobbyId = lobbyId;
                    localUserName = playerName;

                    yield return StartCoroutine(JoinRelayLobbyCoroutine(lobby.relayJoinCode));

                    if (!networkManager.IsHost)
                    {
                        if (clientHeartbeatRoutine != null) StopCoroutine(clientHeartbeatRoutine);
                        clientHeartbeatRoutine = StartCoroutine(SendClientHeartbeatRoutine());
                    }
                    ShowLobbyUI(lobby);
                    RefreshLobbyList();
                }
                else
                {
                    SendNotification("Failed to join the lobby. It might be full or no longer available.", 1);
                }
            }
        }
        finally
        {
            isJoiningRoom = false;
            EnableAllJoinButtons();
        }
    }

    private IEnumerator JoinRelayLobbyCoroutine(string joinCode)
    {
        SendNotification("Connecting to the lobby...", 2);

        bool joinSuccess = false;
        yield return StartCoroutine(JoinRelayCoroutineWrapper(joinCode, (success) => joinSuccess = success));

        if (joinSuccess)
        {
            bool clientStarted = networkManager.StartClient();
            if (clientStarted)
            {
                mainPanel.SetActive(false);
                yield return new WaitForSeconds(2f);
                UIManager.Instance?.CloseNotification();
            }
            else
            {
                isJoiningRoom = false;
                SendNotification("Failed to start client. Please try again.", 1);
                HandleExitLogic();
                ResetUIState();
            }
        }
        else
        {
            isJoiningRoom = false;
            SendNotification("Failed to connect to the lobby. Please check the Lobby ID and try again.", 1);
            HandleExitLogic();
            ResetUIState();
        }
    }

    private IEnumerator JoinRelayCoroutineWrapper(string joinCode, Action<bool> callback)
    {
        bool completed = false;
        bool result = false;

        StartCoroutine(ExecuteAsync(async () =>
        {
            result = await JoinRelayAllocation(joinCode);
            completed = true;
        }));

        yield return new WaitUntil(() => completed);
        callback(result);
    }

    private IEnumerator ExecuteAsync(Func<Task> asyncMethod)
    {
        var task = asyncMethod();
        yield return new WaitUntil(() => task.IsCompleted);
    }

    private async Task<string> CreateRelayAllocation(int maxConnections = 5)
    {
        if (!isUnityServicesInitialized) return null;

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
            transport.SetRelayServerData(relayServerData);
            currentRelayJoinCode = joinCode;

            UIManager.Instance?.CloseNotification();
            return joinCode;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Failed to create lobby: {e.Message}");
            return null;
        }
    }

    private async Task<bool> JoinRelayAllocation(string joinCode)
    {
        if (!isUnityServicesInitialized) return false;

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");
            transport.SetRelayServerData(relayServerData);
            return true;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Failed to join lobby: {e.Message}");
            return false;
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

    private void OnServerStarted()
    {
        Debug.Log("Server started successfully");
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected");
        if (pollLobbyRoutine != null) StopCoroutine(pollLobbyRoutine);
        pollLobbyRoutine = StartCoroutine(PollLobbyInfo());
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.LogWarning($"Client {clientId} disconnected. IsServer={NetworkManager.Singleton.IsServer}");

        if (!isIntentionalDisconnect && GameManager.Instance.IsGameStarted &&
            !NetworkManager.Singleton.IsServer &&
            !NetworkManager.Singleton.IsHost &&
            !NetworkManager.Singleton.ShutdownInProgress)
        {
            if (gameplayCanvas != null)
            {
                gameplayCanvas.OnExitGame(true);
            }
            else
            {
                UIManager.Instance?.SendNotification("Host has left the room. The game has ended.", 1);
                UIManager.Instance?.OpenNotification();
                HandleClientDisconnect();
            }
            return;
        }

        if (!NetworkManager.Singleton.ShutdownInProgress)
        {
            StartCoroutine(PollLobbyInfo());
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            CleanupPlayerObjects(clientId);
        }
        else if (!isIntentionalDisconnect)
        {
            HandleClientDisconnect();
        }
    }

    private void CleanupPlayerObjects(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (NetworkManager.Singleton.SpawnManager == null) return;

        foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
            if (netObj != null && netObj.OwnerClientId == clientId)
            {
                netObj.Despawn();
            }
        }
    }

    private void HandleClientDisconnect()
    {
        if (!string.IsNullOrEmpty(currentLobbyId) && !string.IsNullOrEmpty(localUserName))
        {
            string playerId = PlayerPrefs.GetString("PlayerId", "");
            if (!string.IsNullOrEmpty(playerId))
            {
                var user = new UserInfo { userId = playerId, userName = localUserName };
                StartCoroutine(LeaveLobbyRoutine(currentLobbyId, user));
            }
        }
        ResetState();
        ResetUIState();
        RefreshLobbyList();
    }

    private void OnBackToMenu()
    {
        lobbyPanel.SetActive(false);
        if (joinByIdPanel != null) joinByIdPanel.SetActive(false);
        GameManager.Instance.ShowPlayerPreview();
        UIManager.Instance?.CloseNotification();
        UIManager.Instance?.CloseNetwork();
        UIManager.Instance?.OpenMainMenu();
    }

    private void ClearContainer(Transform container)
    {
        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
    }

    public Sprite GetAvatarForUser(string userId)
    {
        if (availableAvatars == null || availableAvatars.Length == 0)
            return null;

        string localPlayerId = PlayerPrefs.GetString("PlayerId", "");
        if (userId == localPlayerId)
        {
            int avatarIndex = PlayerPrefs.GetInt("PlayerAvatar", 0);
            if (avatarIndex >= 0 && avatarIndex < availableAvatars.Length)
            {
                return availableAvatars[avatarIndex];
            }
        }

        int index = Mathf.Abs(userId.GetHashCode()) % availableAvatars.Length;
        return availableAvatars[index];
    }

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

    private IEnumerator StartGameOnServerRoutine()
    {
        if (string.IsNullOrEmpty(currentLobbyId)) yield break;

        using (var www = new UnityWebRequest($"{SERVER_URL}/{currentLobbyId}/start", "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(new byte[0]);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = 10;

            yield return www.SendWebRequest();
        }
    }

    private IEnumerator SendHeartbeatRoutine()
    {
        while (networkManager != null && networkManager.IsHost && !networkManager.ShutdownInProgress)
        {
            using (var www = UnityWebRequest.PostWwwForm($"{SERVER_URL}/{currentLobbyId}/heartbeat", ""))
            {
                www.timeout = 10;
                yield return www.SendWebRequest();
            }
            yield return new WaitForSeconds(5f);
        }
    }

    private void OnConfirmRoomNameChange()
    {
        if (roomNameInputField == null) return;

        string newName = roomNameInputField.text.Trim();

        if (!Regex.IsMatch(newName, @"^[a-zA-Z0-9 ]+$"))
        {
            SendNotification("Please use only letters and numbers. Special characters are not allowed.", 1);
            return;
        }
        if (string.IsNullOrEmpty(newName))
        {
            SendNotification("Room name cannot be empty", 1);
            return;
        }

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
            requestingUserId = PlayerPrefs.GetString("PlayerId", "")
        };

        string json = JsonUtility.ToJson(reqObj);
        string url = $"{SERVER_URL}/{lobbyId}/rename";

        using (var www = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.timeout = 10;

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

    private IEnumerator SendClientHeartbeatRoutine()
    {
        while (networkManager != null && networkManager.IsClient && !networkManager.ShutdownInProgress)
        {
            if (!string.IsNullOrEmpty(currentLobbyId))
            {
                string playerId = PlayerPrefs.GetString("PlayerId", "");
                if (!string.IsNullOrEmpty(playerId))
                {
                    var payload = new ClientHeartbeatRequest { UserId = playerId };
                    string json = JsonUtility.ToJson(payload);

                    using (var www = new UnityWebRequest($"{SERVER_URL}/{currentLobbyId}/client-heartbeat", "POST"))
                    {
                        www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                        www.downloadHandler = new DownloadHandlerBuffer();
                        www.SetRequestHeader("Content-Type", "application/json");
                        www.timeout = 10;

                        yield return www.SendWebRequest();
                    }
                }
            }
            yield return new WaitForSeconds(5f);
        }
    }

    private IEnumerator AutoRefreshLobbyList()
    {
        while (true)
        {
            if (mainPanel.activeInHierarchy)
            {
                RefreshLobbyList();
            }
            yield return new WaitForSeconds(3f);
        }
    }

    private void SendNotification(string message, int type)
    {
        UIManager.Instance?.SendNotification(message, type);
        UIManager.Instance?.OpenNotification();
    }

    private void ResetNetworkManager()
    {
        if (networkManager == null) return;

        if (networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        if (transport != null)
        {
            transport.SetConnectionData("127.0.0.1", 7777);
        }

        StartCoroutine(ResetNetworkManagerCoroutine());
    }

    private IEnumerator ResetNetworkManagerCoroutine()
    {
        yield return null;

        if (networkManager != null)
        {
            networkManager.gameObject.SetActive(false);
            yield return null;
            networkManager.gameObject.SetActive(true);
        }
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        if (clientHeartbeatRoutine != null)
        {
            clientHeartbeatRoutine = null;
        }

        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnServerStarted -= OnServerStarted;
        }
    }

    private void OnApplicationQuit()
    {
        Debug.Log("Application quitting - cleaning up network...");

        try
        {
            if (heartbeatRoutine != null)
            {
                StopCoroutine(heartbeatRoutine);
                heartbeatRoutine = null;
            }
            if (pollLobbyRoutine != null)
            {
                StopCoroutine(pollLobbyRoutine);
                pollLobbyRoutine = null;
            }
            if (clientHeartbeatRoutine != null)
            {
                clientHeartbeatRoutine = null;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (NetworkManager.Singleton.IsHost)
                {
                    if (!string.IsNullOrEmpty(currentLobbyId))
                    {
                        SendDeleteLobbySync(currentLobbyId);
                    }
                }
                else if (NetworkManager.Singleton.IsClient)
                {
                    if (!string.IsNullOrEmpty(currentLobbyId) && !string.IsNullOrEmpty(localUserName))
                    {
                        string playerId = PlayerPrefs.GetString("PlayerId", "");
                        if (!string.IsNullOrEmpty(playerId))
                        {
                            var user = new UserInfo { userId = playerId, userName = localUserName };
                            SendLeaveLobbySync(currentLobbyId, user);
                        }
                    }
                }

                NetworkManager.Singleton.Shutdown();
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.StopPowerupSpawning();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Error during OnApplicationQuit cleanup: {e.Message}");
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
    
    // ✅ Thêm ở đây, vẫn nằm trong class ConnectionCanvas
    private void DisableAllJoinButtons()
    {
        foreach (var lobbyItem in cachedLobbyItems)
        {
            if (lobbyItem != null && lobbyItem.gameObject.activeSelf)
                lobbyItem.SetJoinButtonInteractable(false);
        }
    }

    private void EnableAllJoinButtons()
    {
        foreach (var lobbyItem in cachedLobbyItems)
        {
            if (lobbyItem != null && lobbyItem.gameObject.activeSelf)
                lobbyItem.SetJoinButtonInteractable(true);
        }
    }

}

[Serializable]
public class RelayLobbyInfo
{
    public string lobbyId;
    public string lobbyName;
    public string relayJoinCode;
    public int currentPlayers;
    public int maxPlayers;
    public string createdAt;
    public string hostUserId;
    public bool isGameStarted;
    public List<UserInfo> users = new List<UserInfo>();
}

[Serializable]
public class RelayLobbyRegistrationRequest
{
    public string lobbyName;
    public string relayJoinCode;
    public int maxPlayers;
    public string hostName;
    public int AvatarIndex;
    public string HostUserId;
}

[Serializable]
public class UserInfo
{
    public string userId;
    public string userName;
    public int avatarIndex;
    public ulong clientId;
    public bool isReady;
}

[Serializable]
public class UpdateReadyRequest
{
    public string UserId;
    public bool IsReady;
}

[Serializable]
public class UpdateLobbyNameRequest
{
    public string lobbyId;
    public string lobbyName;
    public string requestingUserId;
}

[Serializable]
public class ClientHeartbeatRequest
{
    public string UserId;
}

public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        string newJson = "{ \"array\": " + json + "}";
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
        return wrapper.array;
    }

    [Serializable]
    private class Wrapper<T>
    {
        public T[] array;
    }
    
    
}