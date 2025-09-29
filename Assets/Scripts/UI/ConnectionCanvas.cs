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
    [SerializeField] private GameObject lobbyItemPrefab;

    [Header("Lobby Panel")]
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private TextMeshProUGUI lobbyId;
    [SerializeField] private GameObject playerItemPrefab;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button settingButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button editProfileInLobbyButton;

    [Header("Join by ID Panel")]
    [SerializeField] private GameObject joinByIdPanel;
    [SerializeField] private TMP_InputField lobbyIdInputField;
    [SerializeField] private Button confirmJoinButton;
    [SerializeField] private Button cancelJoinButton;

    [Header("Player Info Panel")]
    [SerializeField] private TMP_InputField playerNameInputField;
    [SerializeField] private Transform avatarSelectionContainer;
    [SerializeField] private GameObject avatarItemPrefab;
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

    // Server URL updated for relay support
    private const string SERVER_URL = "https://mini-server-8.onrender.com/api/lobby";
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
    private bool isSinglePlayerMode = false;
    private RelayLobbyInfo currentLobbyInfo;
    private bool isUnityServicesInitialized = false;

    private async void Start()
    {
        InitializeButtons();
        InitializeNetworkCallbacks();
        SetInitialUIState();
        LoadPlayerPrefs();

        // Initialize Unity Services for Relay
        await InitializeUnityServices();

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

    private async Task<string> CreateRelayAllocation(int maxConnections = 5)
    {
        if (!isUnityServicesInitialized)
        {
            Debug.LogError("Unity Services not initialized");
            return null;
        }

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
            transport.SetRelayServerData(relayServerData);
            UIManager.Instance?.CloseNotification();
            currentRelayJoinCode = joinCode;
            Debug.Log($"Relay allocation created. Join Code: {joinCode}");

            return joinCode;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Failed to create relay allocation: {e.Message}");
            return null;
        }
    }

    private async Task<bool> JoinRelayAllocation(string joinCode)
    {
        if (!isUnityServicesInitialized)
        {
            Debug.LogError("Unity Services not initialized");
            return false;
        }

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");
            transport.SetRelayServerData(relayServerData);

            Debug.Log($"Successfully joined relay with code: {joinCode}");
            return true;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Failed to join relay: {e.Message}");
            return false;
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

        for (int i = 0; i < availableAvatars.Length; i++)
        {
            var avatarItemObj = Instantiate(avatarItemPrefab, avatarSelectionContainer);
            if (avatarItemObj == null) continue;

            var avatarItem = avatarItemObj.GetComponent<AvatarItem>();
            if (avatarItem == null) continue;

            avatarItem.Initialize(i, availableAvatars[i], SelectAvatar);
        }

        SelectAvatar(Mathf.Clamp(selectedAvatarIndex, 0, availableAvatars.Length - 1));
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

    private void SelectAvatar(int index)
    {
        selectedAvatarIndex = index;
        for (int i = 0; i < avatarSelectionContainer.childCount; i++)
        {
            var avatarItem = avatarSelectionContainer.GetChild(i).GetComponent<AvatarItem>();
            if (avatarItem != null)
            {
                avatarItem.SetHighlight(i == index);
            }
        }
    }

    private void LoadPlayerPrefs()
    {
        localUserName = PlayerPrefs.GetString("PlayerName", DEFAULT_PLAYER_NAME);
        selectedAvatarIndex = PlayerPrefs.GetInt("PlayerAvatar", 0);
        SelectAvatar(selectedAvatarIndex);
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
        {
            gameplayCanvas.Init(this, "SINGLE", localUserName);
        }
    }

    private async void OnOnlineClicked()
    {
        isSinglePlayerMode = false;
        UIManager.Instance?.SendNotification("Checking connection...");
        UIManager.Instance?.OpenNotification();
        if (!isUnityServicesInitialized)
        {
            await InitializeUnityServices();
        }

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
        }
        else
        {
            string message = !isUnityServicesInitialized ?
                "Unable to connect to online services. Please check your internet connection and try again." :
                "Connection failed. Please check your internet connection.";

            UIManager.Instance?.SendNotification(message);
            UIManager.Instance?.OpenNotification();
        }
    }

    private void OnBackToModePanel()
    {
        mainPanel.SetActive(false);
        modePanel.SetActive(true);
    }

    private void OnStartHostClicked()
    {
        ResetNetworkManager(); 
        StartHost();
    }

    private async void StartHost()
    {
        if (!isUnityServicesInitialized)
        {
            UIManager.Instance?.SendNotification("Online services are not ready. Please try again.");
            UIManager.Instance?.OpenNotification();
            return;
        }

        UIManager.Instance?.SendNotification("Creating lobby...");
        UIManager.Instance?.OpenNotification();

        string joinCode = await CreateRelayAllocation(6);
        if (string.IsNullOrEmpty(joinCode))
        {
            UIManager.Instance?.SendNotification("Failed to create game session. Please try again.");
            UIManager.Instance?.OpenNotification();
            ResetUIState();
            return;
        }

        if (!networkManager.StartHost())
        {
            UIManager.Instance?.SendNotification("Failed to start hosting. Please try again.");
            UIManager.Instance?.OpenNotification();
            ResetUIState();
            return;
        }

        StartCoroutine(StartHostRoutineCoroutine(localUserName, localUserName + "'s " + DEFAULT_LOBBY_NAME, joinCode));
        mainPanel.SetActive(false);
    }

    private IEnumerator StartHostRoutineCoroutine(string hostName, string lobbyName, string joinCode)
    {
        string hostId = PlayerPrefs.GetString("PlayerId", "");
        var request = new RelayLobbyRegistrationRequest
        {
            lobbyName = lobbyName,
            relayJoinCode = joinCode,
            maxPlayers = 6,
            hostName = hostName,
            AvatarIndex = PlayerPrefs.GetInt("PlayerAvatar", 0)
        };

        bool serverAvailable = false;
        yield return StartCoroutine(CheckServerAvailabilityCoroutine((result) => serverAvailable = result));

        if (serverAvailable)
        {
            yield return StartCoroutine(RegisterRelayLobbyOnServer(request));
        }
        else
        {
            Debug.LogWarning("Server not available, starting offline mode");
            currentLobbyId = "LOCAL";
            localUserName = hostName;
            mainPanel.SetActive(false);
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
                ShowLobbyUI(lobby);
                heartbeatRoutine = StartCoroutine(SendHeartbeatRoutine());
                pollLobbyRoutine = StartCoroutine(PollLobbyInfo());
            }
            else
            {
                Debug.LogWarning("Could not reach lobby server, starting local offline host");
                currentLobbyId = "LOCAL";
                localUserName = DEFAULT_PLAYER_NAME;
                mainPanel.SetActive(false);
            }
        }
    }

    public void ShowJoinByIdPanel()
    {
        if (joinByIdPanel != null)
        {
            joinByIdPanel.SetActive(true);
            lobbyIdInputField.text = string.Empty;
        }
    }

    private void CloseJoinByIdPanel()
    {
        if (joinByIdPanel != null)
        {
            joinByIdPanel.SetActive(false);
        }
    }

    private void OnConfirmJoinById()
    {
        string lobbyId = lobbyIdInputField.text.Trim();
        if (!Regex.IsMatch(lobbyId, @"^[a-zA-Z0-9 ]+$"))
        {
            UIManager.Instance?.SendNotification("Please use only letters and numbers. Special characters are not allowed.");
            UIManager.Instance?.OpenNotification();
            return;
        }
        if (string.IsNullOrEmpty(lobbyId))
        {
            UIManager.Instance?.SendNotification("Please enter a lobby ID");
            UIManager.Instance?.OpenNotification();
            return;
        }

        CloseJoinByIdPanel();

        var tmp = new RelayLobbyInfo { lobbyId = lobbyId };
        StartCoroutine(JoinLobbyRoutine(tmp.lobbyId, localUserName));
    }

    public void JoinLobbyDirect(RelayLobbyInfo lobby)
    {
        if (lobby == null) return;
        ResetNetworkManager();
        StartCoroutine(JoinLobbyRoutine(lobby.lobbyId, localUserName));
    }

    private void ShowEditProfilePanel()
    {
        if (playerInfoPanel != null)
        {
            playerInfoPanel.SetActive(true);
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
            UIManager.Instance?.SendNotification("Please use only letters and numbers. Special characters are not allowed.");
            UIManager.Instance?.OpenNotification();
            return;
        }
        if (string.IsNullOrEmpty(newName))
        {
            UIManager.Instance?.SendNotification("Please enter your name");
            UIManager.Instance?.OpenNotification();
            return;
        }

        localUserName = newName;
        SavePlayerPrefs(localUserName);
        HidePlayerInfoPanel();

        // Cập nhật thông tin người chơi trong lobby nếu đang trong lobby
        if (!string.IsNullOrEmpty(currentLobbyId) && networkManager.IsClient)
        {
            StartCoroutine(UpdatePlayerInfoInLobby());
        }

        UIManager.Instance?.SendNotification("Profile updated successfully");
        UIManager.Instance?.OpenNotification();
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
            StartCoroutine(StartGameOnServerRoutine());
            GameManager.Instance.StartGame();
            GameManager.Instance.StartGameClientRpc();
            GameManager.Instance.StartPowerupSpawning();
        }
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
    }

    private void ResetState()
    {
        currentLobbyId = string.Empty;
        currentRelayJoinCode = string.Empty;
        isSinglePlayerMode = false;
        currentLobbyInfo = null;
    }

    private void ResetUIState()
    {
        lobbyPanel.SetActive(false);
        if (joinByIdPanel != null) joinByIdPanel.SetActive(false);
        if (playerInfoPanel != null) playerInfoPanel.SetActive(false);
        if (settingPanel != null) settingPanel.SetActive(false);
        mainPanel.SetActive(true);
        modePanel.SetActive(false);
    }

    private void ShowLobbyUI(RelayLobbyInfo lobby)
    {
        currentLobbyInfo = lobby;
        lobbyPanel.SetActive(true);
        lobbyId.text = $"Lobby ID: {lobby.lobbyId}";
        UpdatePlayerList(lobby);
        startGameButton.interactable = networkManager.IsHost;
    }

    private void UpdatePlayerList(RelayLobbyInfo lobby)
    {
        ClearContainer(playerListContainer);

        if (lobby?.users == null) return;

        foreach (var user in lobby.users)
        {
            var item = Instantiate(playerItemPrefab, playerListContainer);
            var comp = item.GetComponent<PlayerItem>();
            if (comp != null)
            {
                bool canKick = networkManager.IsHost && user.userId != PlayerPrefs.GetString("PlayerId", "");
                Sprite avatar = null;
                if (user.avatarIndex >= 0 && user.avatarIndex < availableAvatars.Length)
                    avatar = availableAvatars[user.avatarIndex];

                comp.Setup(user.userName, user.userId, user.clientId, this, canKick, avatar);
            }
        }

        if (gameplayCanvas != null)
        {
            gameplayCanvas.UpdateExitButtonState(lobby);
        }
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
        using (var checkWww = UnityWebRequest.Get($"{SERVER_URL}/find/{lobbyId}"))
        {
            checkWww.timeout = 10;
            yield return checkWww.SendWebRequest();

            if (checkWww.result != UnityWebRequest.Result.Success)
            {
                UIManager.Instance?.SendNotification("Lobby not found. Please check the ID and try again.");
                UIManager.Instance?.OpenNotification();
                yield break;
            }

            RelayLobbyInfo lobbyCheck = JsonUtility.FromJson<RelayLobbyInfo>(checkWww.downloadHandler.text);
            if (lobbyCheck == null)
            {
                UIManager.Instance?.SendNotification("Lobby not found. Please check the ID and try again.");
                UIManager.Instance?.OpenNotification();
                yield break;
            }

            if (lobbyCheck.isGameStarted)
            {
                UIManager.Instance?.SendNotification("This game has already started. You cannot join a game in progress.");
                UIManager.Instance?.OpenNotification();
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
                ShowLobbyUI(lobby);

                // Join using relay
                yield return StartCoroutine(JoinRelayLobbyCoroutine(lobby.relayJoinCode));

                mainPanel.SetActive(false);
                RefreshLobbyList();
            }
            else
            {
                UIManager.Instance?.SendNotification("Failed to join lobby. The lobby may be full or no longer available.");
                UIManager.Instance?.OpenNotification();
            }
        }
    }

    private IEnumerator JoinRelayLobbyCoroutine(string joinCode)
    {
        UIManager.Instance?.SendNotification("Connecting to game...");
        UIManager.Instance?.OpenNotification();

        bool joinSuccess = false;
        yield return StartCoroutine(JoinRelayCoroutineWrapper(joinCode, (success) => joinSuccess = success));

        if (joinSuccess)
        {
            bool clientStarted = networkManager.StartClient();
            if (clientStarted)
            {
                UIManager.Instance?.CloseNotification();
            }
            else
            {
                UIManager.Instance?.SendNotification("Failed to connect to the game. Please try again.");
                UIManager.Instance?.OpenNotification();
            }
        }
        else
        {
            UIManager.Instance?.SendNotification("Connection failed. Please check your internet connection.");
            UIManager.Instance?.OpenNotification();
        }
    }

    private IEnumerator JoinRelayCoroutineWrapper(string joinCode, System.Action<bool> callback)
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

    private IEnumerator ExecuteAsync(System.Func<Task> asyncMethod)
    {
        var task = asyncMethod();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Exception != null)
        {
            Debug.LogError($"Async task failed: {task.Exception}");
        }
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
                ClearContainer(lobbyListContainer);

                foreach (var lobby in lobbies)
                {
                    var item = Instantiate(lobbyItemPrefab, lobbyListContainer);
                    var comp = item.GetComponent<LobbyItem>();
                    if (comp != null)
                        comp.Setup(lobby, this);
                }
            }
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

    private void OnServerStarted()
    {
        Debug.Log("Server started successfully");
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client {clientId} connected");
        if (networkManager.IsHost)
        {
            if (pollLobbyRoutine != null) StopCoroutine(pollLobbyRoutine);
            pollLobbyRoutine = StartCoroutine(PollLobbyInfo());
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.LogWarning($"Client {clientId} disconnected. IsServer={NetworkManager.Singleton.IsServer}");

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            CleanupPlayerObjects(clientId);
            if (!NetworkManager.Singleton.ShutdownInProgress)
            {
                StartCoroutine(PollLobbyInfo());
            }
        }
        else
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
                netObj.gameObject.SetActive(false);
            }
        }
    }

    private void HandleClientDisconnect()
    {
        HandleExitLogic();
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
        while (networkManager != null && networkManager.IsHost && !networkManager.ShutdownInProgress)
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
            UIManager.Instance?.SendNotification("Please use only letters and numbers. Special characters are not allowed.");
            UIManager.Instance?.OpenNotification();
            return;
        }
        if (string.IsNullOrEmpty(newName))
        {
            UIManager.Instance?.SendNotification("Room name cannot be empty");
            UIManager.Instance?.OpenNotification();
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
                UIManager.Instance?.SendNotification("Room name updated successfully");
                UIManager.Instance?.OpenNotification();
                StartCoroutine(RefreshCurrentLobbyInfo());
            }
            else
            {
                UIManager.Instance?.SendNotification("Failed to update room name. Please try again.");
                UIManager.Instance?.OpenNotification();
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

    private void OnDestroy()
    {
        StopAllCoroutines();

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

        Debug.Log("NetworkManager reset completed");
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
}

[Serializable]
public class UserInfo
{
    public string userId;
    public string userName;
    public int avatarIndex;
    public ulong clientId;
}

[Serializable]
public class UpdateLobbyNameRequest
{
    public string lobbyId;
    public string lobbyName;
    public string requestingUserId;
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