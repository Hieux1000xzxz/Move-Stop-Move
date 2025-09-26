using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
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
    [SerializeField] private Transform lobbyListContainer;
    [SerializeField] private GameObject lobbyItemPrefab;

    [Header("Lobby Panel")]
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private TextMeshProUGUI lobbyId;
    [SerializeField] private GameObject playerItemPrefab;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button settingButton;
    [SerializeField] private Button exitButton;

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

    private const string SERVER_URL = "http://192.168.1.32:5000/api/lobby";
    private const string DEFAULT_LOBBY_NAME = "Lobby";
    private const string DEFAULT_PLAYER_NAME = "You";
    private int selectedAvatarIndex = 0;
    private string currentLobbyId = string.Empty;
    private string localUserName = DEFAULT_PLAYER_NAME;
    private Coroutine heartbeatRoutine;
    private Coroutine pollLobbyRoutine;
    private Coroutine autoRefreshLobbyRoutine;
    private LobbyInfo pendingLobbyJoin;
    private bool isSinglePlayerMode = false;
    private LobbyInfo currentLobbyInfo;


    private void Start()
    {
        InitializeButtons();
        InitializeNetworkCallbacks();
        SetInitialUIState();
        LoadPlayerPrefs();
        RefreshLobbyList();
        InitializeAvatarSelection();
        autoRefreshLobbyRoutine = StartCoroutine(AutoRefreshLobbyList());
    }

    private IEnumerator AutoRefreshLobbyList()
    {
        while (true)
        {
            RefreshLobbyList();
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

        startGameButton.onClick.AddListener(OnStartGameClicked);
        settingButton.onClick.AddListener(ShowSettingPanel);
        exitButton.onClick.AddListener(OnExitClicked);

        confirmJoinButton.onClick.AddListener(OnConfirmJoinById);
        cancelJoinButton.onClick.AddListener(CloseJoinByIdPanel);

        confirmRoomNameButton.onClick.AddListener(OnConfirmRoomNameChange);
        closeSettingButton.onClick.AddListener(HideSettingPanel);

        confirmPlayerInfoButton.onClick.AddListener(OnConfirmPlayerInfo);
        cancelPlayerInfoButton.onClick.AddListener(OnCancelPlayerInfo);

    }
    private void InitializeAvatarSelection()
    {
        if (avatarSelectionContainer == null)
        {
            Debug.LogError("Avatar Selection Container is not assigned in Inspector!");
            return;
        }

        if (availableAvatars == null || availableAvatars.Length == 0)
        {
            Debug.LogWarning("No available avatars assigned. Using default colors instead.");
            CreateDefaultAvatarSelection();
            return;
        }

        if (avatarItemPrefab == null)
        {
            Debug.LogError("Avatar Item Prefab is not assigned in Inspector!");
            return;
        }

        ClearContainer(avatarSelectionContainer);

        for (int i = 0; i < availableAvatars.Length; i++)
        {
            var avatarItemObj = Instantiate(avatarItemPrefab, avatarSelectionContainer);

            if (avatarItemObj == null) continue;

            // Get AvatarItem component
            var avatarItem = avatarItemObj.GetComponent<AvatarItem>();
            if (avatarItem == null)
            {
                Debug.LogWarning("AvatarItem component not found on prefab!");
                continue;
            }

            // Initialize với avatarItem component
            avatarItem.Initialize(i, availableAvatars[i], SelectAvatar);
        }

        SelectAvatar(Mathf.Clamp(selectedAvatarIndex, 0, availableAvatars.Length - 1));
    }

    public void ShowSettingPanel()
    {
        if (settingPanel != null)
        {
            settingPanel.SetActive(true);

            // Gợi ý: hiện room name hiện tại
            if (roomNameInputField != null && currentLobbyInfo != null)
                roomNameInputField.text = currentLobbyInfo.lobbyName;
        }
    }

    public void HideSettingPanel()
    {
        if (settingPanel != null)
            settingPanel.SetActive(false);
    }

    private void CreateDefaultAvatarSelection()
    {
        if (avatarSelectionContainer == null) return;

        for (int i = 0; i < 6; i++)
        {
            var avatarItemObj = Instantiate(avatarItemPrefab, avatarSelectionContainer);
            if (avatarItemObj == null) continue;

            var avatarItem = avatarItemObj.GetComponent<AvatarItem>();
            if (avatarItem == null) continue;

            //Sprite defaultSprite = CreateDefaultSprite(GetColorByIndex(i));
            //avatarItem.Initialize(i, defaultSprite, SelectAvatar);
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
        // Load player name từ PlayerPrefs
        localUserName = PlayerPrefs.GetString("PlayerName", DEFAULT_PLAYER_NAME);
        selectedAvatarIndex = PlayerPrefs.GetInt("PlayerAvatar", 0);

        if (playerNameInputField != null)
        {
            playerNameInputField.text = localUserName;
        }
        SelectAvatar(selectedAvatarIndex);
    }

    private void SavePlayerPrefs()
    {
        PlayerPrefs.SetString("PlayerName", localUserName);
        PlayerPrefs.SetInt("PlayerAvatar", selectedAvatarIndex);
        PlayerPrefs.Save();
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

    private void ShowPlayerInfoPanel(string purpose = "host")
    {
        if (playerInfoPanel != null)
        {
            playerInfoPanel.SetActive(true);
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
            UIManager.Instance?.SendNotification("Special characters are not allowed!");
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

        // Save PlayerId, name và avatar
        SavePlayerPrefs(localUserName);

        HidePlayerInfoPanel();

        if (pendingLobbyJoin != null)
        {
            StartCoroutine(JoinLobbyRoutine(pendingLobbyJoin.lobbyId, localUserName));
            pendingLobbyJoin = null;
        }
        else
        {
            StartHost();
        }
    }

    private void OnCancelPlayerInfo()
    {
        HidePlayerInfoPanel();
        pendingLobbyJoin = null;
        mainPanel.SetActive(true);
    }

    private void SetInitialUIState()
    {
        modePanel.SetActive(true);
        lobbyPanel.SetActive(false);
        mainPanel.SetActive(false);
        if (joinByIdPanel != null) joinByIdPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        Application.quitting -= OnApplicationQuitting;

        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnServerStarted -= OnServerStarted;
        }
    }

    private void OnApplicationQuitting()
    {
        HandleExitLogic();
    }

    private void OnSinglePlayerClicked()
    {
        isSinglePlayerMode = true;
        localUserName = DEFAULT_PLAYER_NAME;
        currentLobbyId = "SINGLE";

        // Initialize single player mode
        if (networkManager != null)
        {
            // Start as host for single player to enable spawning
            transport.SetConnectionData("127.0.0.1", 7777);
            networkManager.StartHost();
        }

        // Start game directly without network lobby
        StartCoroutine(StartSinglePlayerGame());

        modePanel.SetActive(false);
    }

    private IEnumerator StartSinglePlayerGame()
    {
        // Wait a frame for network to initialize
        yield return null;

        GameManager.Instance.StartGame();
        GameManager.Instance.StartPowerupSpawning();

        if (gameplayCanvas != null)
        {
            gameplayCanvas.Init(this, "SINGLE", localUserName);
        }
    }

    private void OnOnlineClicked()
    {
        isSinglePlayerMode = false;
        StartCoroutine(CheckNetworkAndShowMainPanel());
    }

    private IEnumerator CheckNetworkAndShowMainPanel()
    {
        //UIManager.Instance?.SendNotification("Checking network connection and server availability...");
        //UIManager.Instance?.OpenNotification();

        bool serverAvailable = false;
        yield return StartCoroutine(CheckServerAvailabilityCoroutine((result) => serverAvailable = result));

        if (serverAvailable)
        {
            //UIManager.Instance?.SendNotification("Server connection successful!");
            //UIManager.Instance?.OpenNotification();
            modePanel.SetActive(false);
            mainPanel.SetActive(true);
        }
        else
        {
            UIManager.Instance?.SendNotification("Unable to connect to server. Please check your internet connection.");
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
        ShowPlayerInfoPanel("host");
    }

    private void StartHost()
    {
        StartCoroutine(StartHostRoutine(localUserName, DEFAULT_LOBBY_NAME));
        mainPanel.SetActive(false);
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
            UIManager.Instance?.SendNotification("Special characters are not allowed!");
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

        var tmp = new LobbyInfo { lobbyId = lobbyId };
        JoinLobbyDirect(tmp);
    }


    public void JoinLobbyDirect(LobbyInfo lobby)
    {
        if (lobby == null) return;

        pendingLobbyJoin = lobby;
        ShowPlayerInfoPanel("join");
    }

    private void OnStartGameClicked()
    {
        if (networkManager.IsHost)
        {
            //if (currentLobbyInfo == null || currentLobbyInfo.users == null || currentLobbyInfo.users.Count < 2)
            //{
            //    UIManager.Instance?.SendNotification("At least 2 players are required to start the game!");
            //    UIManager.Instance?.OpenNotification();
            //    return;
            //}

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
        localUserName = DEFAULT_PLAYER_NAME;
        isSinglePlayerMode = false;
    }

    private void ResetUIState()
    {
        lobbyPanel.SetActive(false);
        if (joinByIdPanel != null) joinByIdPanel.SetActive(false);
        mainPanel.SetActive(true);
        modePanel.SetActive(false);
    }

    private IEnumerator RegisterLobbyOnServer(LobbyRegistrationRequest request)
    {
        string json = JsonUtility.ToJson(request);

        using (var www = new UnityWebRequest($"{SERVER_URL}/register", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var lobby = JsonUtility.FromJson<LobbyInfo>(www.downloadHandler.text);
                currentLobbyId = lobby.lobbyId;
                localUserName = DEFAULT_PLAYER_NAME;
                ShowLobbyUI(lobby);
                heartbeatRoutine = StartCoroutine(SendHeartbeatRoutine());
                StartCoroutine(PollLobbyInfo());
            }
            else
            {
                Debug.LogWarning("⚠️ Could not reach lobby server, starting local offline host...");

                currentLobbyId = "LOCAL";
                localUserName = DEFAULT_PLAYER_NAME;
                mainPanel.SetActive(false);
            }
        }
    }


    private void ShowLobbyUI(LobbyInfo lobby)
    {
        currentLobbyInfo = lobby;
        lobbyPanel.SetActive(true);
        lobbyId.text = $"Lobby ID: {lobby.lobbyId}";
        UpdatePlayerList(lobby);
        startGameButton.interactable = networkManager.IsHost;
    }

    private void UpdatePlayerList(LobbyInfo lobby)
    {
        ClearContainer(playerListContainer);

        if (lobby?.users == null) return;

        foreach (var user in lobby.users)
        {
            var item = Instantiate(playerItemPrefab, playerListContainer);
            var comp = item.GetComponent<PlayerItem>();
            if (comp != null)
            {
                bool canKick = networkManager.IsHost;
                Sprite avatar = null;
                if (user.avatarIndex >= 0 && user.avatarIndex < availableAvatars.Length)
                    avatar = availableAvatars[user.avatarIndex];

                comp.Setup(user.userName, user.userId, user.clientId, this, canKick, avatar);
            }
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


    private IEnumerator StartHostRoutine(string hostName, string lobbyName)
    {
        ushort port = 7777;
        string ipLan = GetHostLANIP();
        transport.SetConnectionData("0.0.0.0", port);

        if (!networkManager.StartHost())
        {
            UIManager.Instance?.SendNotification("Failed to start hosting. Please try again.");
            UIManager.Instance?.OpenNotification();
            ResetUIState();
            yield break;
        }

        string hostId = PlayerPrefs.GetString("PlayerId", "");
        var request = new LobbyRegistrationRequest
        {
            lobbyName = lobbyName,
            hostIpAddress = ipLan,
            hostPort = port,
            maxPlayers = 6,
            hostName = hostName,
            AvatarIndex = PlayerPrefs.GetInt("PlayerAvatar", 0)
        };

        bool serverAvailable = false;
        yield return StartCoroutine(CheckServerAvailabilityCoroutine((result) => serverAvailable = result));

        if (serverAvailable)
        {
            yield return StartCoroutine(RegisterLobbyOnServer(request));
        }
        else
        {
            Debug.LogWarning("⚠️ Server not available, starting offline mode...");

            currentLobbyId = "LOCAL";
            localUserName = hostName;
            mainPanel.SetActive(false);
        }
    }

    public Sprite GetAvatarForUser(string userId)
    {
        if (availableAvatars == null || availableAvatars.Length == 0)
            return null;

        // Nếu là local player, lấy avatar đã chọn
        string localPlayerId = PlayerPrefs.GetString("PlayerId", "");
        if (userId == localPlayerId)
        {
            int avatarIndex = PlayerPrefs.GetInt("PlayerAvatar", 0);
            if (avatarIndex >= 0 && avatarIndex < availableAvatars.Length)
            {
                return availableAvatars[avatarIndex];
            }
        }

        // Với remote player, tạo avatar index dựa trên userId
        int index = Mathf.Abs(userId.GetHashCode()) % availableAvatars.Length;
        return availableAvatars[index];
    }


    private IEnumerator CheckServerAvailabilityCoroutine(System.Action<bool> callback)
    {
        using (var www = UnityWebRequest.Get($"{SERVER_URL}/ping"))
        {
            www.timeout = 3;
            yield return www.SendWebRequest();
            callback(www.result == UnityWebRequest.Result.Success);
        }
    }

    private IEnumerator JoinLobbyRoutine(string lobbyId, string playerName)
    {
        using (var checkWww = UnityWebRequest.Get($"{SERVER_URL}/find/{lobbyId}"))
        {
            yield return checkWww.SendWebRequest();

            if (checkWww.result != UnityWebRequest.Result.Success)
            {
                UIManager.Instance?.SendNotification("Lobby not found. Please check the ID.");
                UIManager.Instance?.OpenNotification();
                yield break;
            }

            LobbyInfo lobbyCheck = JsonUtility.FromJson<LobbyInfo>(checkWww.downloadHandler.text);
            if (lobbyCheck == null)
            {
                UIManager.Instance?.SendNotification("Lobby not found. Please check the ID.");
                UIManager.Instance?.OpenNotification();
                yield break;
            }

            if (lobbyCheck.isGameStarted)
            {
                UIManager.Instance?.SendNotification("This game has already started. You cannot join.");
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
            avatarIndex = PlayerPrefs.GetInt("PlayerAvatar", 0)
        };
        string json = JsonUtility.ToJson(user);

        using (var www = new UnityWebRequest($"{SERVER_URL}/{lobbyId}/join", "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                LobbyInfo lobby = JsonUtility.FromJson<LobbyInfo>(www.downloadHandler.text);
                currentLobbyId = lobbyId;
                localUserName = playerName;
                ShowLobbyUI(lobby);
                JoinLobby(lobby.hostIpAddress, lobby.hostPort);
                mainPanel.SetActive(false);
                RefreshLobbyList();
            }
            else
            {
                UIManager.Instance?.SendNotification("Failed to join lobby. Please try again.");
                UIManager.Instance?.OpenNotification();
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

            yield return www.SendWebRequest();
        }
    }

    public void JoinLobby(string ip, int port)
    {
        transport.SetConnectionData(ip, (ushort)port);
        networkManager.StartClient();
    }

    public void RefreshLobbyList()
    {
        StartCoroutine(RefreshLobbyListRoutine());
    }

    private IEnumerator RefreshLobbyListRoutine()
    {
        using (var www = UnityWebRequest.Get($"{SERVER_URL}/list"))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string json = www.downloadHandler.text;
                LobbyInfo[] lobbies = JsonHelper.FromJson<LobbyInfo>(json);
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

    private void OnServerStarted()
    {
    }

    private void OnClientConnected(ulong clientId)
    {
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
        for (int i = container.childCount - 1; i >= 0; i--)
            Destroy(container.GetChild(i).gameObject);
    }

    private string GetHostLANIP()
    {
        try
        {
            foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        string ip = addr.Address.ToString();
                        if (ip.StartsWith("10.") || ip.StartsWith("192.168.") || ip.StartsWith("172."))
                            return ip;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Get LAN IP failed: {e.Message}");
        }
        return "127.0.0.1";
    }

    private IEnumerator PollLobbyInfo()
    {
        while (networkManager != null && networkManager.IsHost && !networkManager.ShutdownInProgress)
        {
            if (!string.IsNullOrEmpty(currentLobbyId))
            {
                using (var www = UnityWebRequest.Get($"{SERVER_URL}/find/{currentLobbyId}"))
                {
                    yield return www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success && this != null && lobbyPanel != null)
                    {
                        LobbyInfo lobby = JsonUtility.FromJson<LobbyInfo>(www.downloadHandler.text);
                        UpdatePlayerList(lobby);
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

            yield return www.SendWebRequest();
        }
    }

    private IEnumerator SendHeartbeatRoutine()
    {
        while (networkManager != null && networkManager.IsHost && !networkManager.ShutdownInProgress)
        {
            using (var www = UnityWebRequest.PostWwwForm($"{SERVER_URL}/{currentLobbyId}/heartbeat", ""))
            {
                yield return www.SendWebRequest();
            }
            yield return new WaitForSeconds(5f);
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

            // Shutdown network trước
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

                // Shutdown network cuối cùng
                NetworkManager.Singleton.Shutdown();
            }

            // Sau khi shutdown network, dọn dẹp game
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StopPowerupSpawning();
            }
#if UNITY_ANDROID || UNITY_IOS
            Application.Quit();
#endif
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
            req.timeout = 2;
            var op = req.SendWebRequest();
            while (!op.isDone) { } // Blocking wait (chỉ dùng khi quit app)
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
            req.timeout = 2;

            var op = req.SendWebRequest();
            while (!op.isDone) { }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Leave lobby failed: {e.Message}");
        }
    }

    private void OnConfirmRoomNameChange()
    {
        if (roomNameInputField == null) return;

        string newName = roomNameInputField.text.Trim();

        if (!Regex.IsMatch(newName, @"^[a-zA-Z0-9 ]+$"))
        {
            UIManager.Instance?.SendNotification("Special characters are not allowed!");
            UIManager.Instance?.OpenNotification();
            return;
        }
        if (string.IsNullOrEmpty(newName))
        {
            UIManager.Instance?.SendNotification("Room name cannot be empty");
            UIManager.Instance?.OpenNotification();
            return;
        }

        // Cập nhật local ngay lập tức (có thể hiện lên UI)
        if (currentLobbyInfo == null) currentLobbyInfo = new LobbyInfo { lobbyId = currentLobbyId };
        currentLobbyInfo.lobbyName = newName;
        //if (lobbyNameText != null) lobbyNameText.text = newName;

        // Gọi server update (endpoint ví dụ: {SERVER_URL}/{lobbyId}/rename)
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

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                UIManager.Instance?.SendNotification("Room name updated");
                UIManager.Instance?.OpenNotification();
                StartCoroutine(RefreshCurrentLobbyInfo());
            }
            else
            {
                UIManager.Instance?.SendNotification("Failed to update room name");
                UIManager.Instance?.OpenNotification();
            }
        }
    }

    private IEnumerator RefreshCurrentLobbyInfo()
    {
        if (string.IsNullOrEmpty(currentLobbyId)) yield break;

        using (var www = UnityWebRequest.Get($"{SERVER_URL}/find/{currentLobbyId}"))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                LobbyInfo lobby = JsonUtility.FromJson<LobbyInfo>(www.downloadHandler.text);
                if (lobby != null)
                {
                    currentLobbyInfo = lobby;
                    //if (lobbyNameText != null) lobbyNameText.text = string.IsNullOrEmpty(lobby.lobbyName) ? DEFAULT_LOBBY_NAME : lobby.lobbyName;
                    UpdatePlayerList(lobby);
                }
            }
        }
    }

}

[Serializable]
public class LobbyInfo
{
    public string lobbyId;
    public string lobbyName;
    public string hostIpAddress;
    public int hostPort;
    public int currentPlayers;
    public int maxPlayers;
    public string createdAt;
    public int hostUserId;
    public bool isGameStarted;
    public List<UserInfo> users = new List<UserInfo>();
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
public class LobbyRegistrationRequest
{
    public string lobbyName;
    public string hostIpAddress;
    public int hostPort;
    public int maxPlayers;
    public string hostName;
    public int AvatarIndex;
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

[Serializable]
public class UpdateLobbyNameRequest
{
    public string lobbyId;
    public string lobbyName;
    public string requestingUserId;
}
