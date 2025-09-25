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
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject enterNamePopup;
    [SerializeField] private GamePlayCanvas gameplayCanvas;

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
    [SerializeField] private Button exitButton;

    [Header("Name Popup")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button confirmNameButton;
    [SerializeField] private TextMeshProUGUI popupTitleText;
    [SerializeField] private Button closeEnterNamePanelButton;

    [Header("Network")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport transport;

    [SerializeField] private GameObject playerPreview;
    [SerializeField] private CinemachineCamera mainCamera;
    [SerializeField] private CinemachineZoomController cinemachineZoom;

    private const string SERVER_URL = "http://192.168.1.32:5000/api/lobby";
    private string currentLobbyId = string.Empty;
    private string localUserName = string.Empty;
    private LobbyInfo pendingLobbyToJoin = null;
    private Coroutine heartbeatRoutine;
    private bool isCreatingLobby = false;
    private enum NamePopupMode
    {
        None,
        EnterLobbyName,
        EnterPlayerName,
        EnterLobbyId,
        JoinLobby
    }

    private NamePopupMode currentPopupMode = NamePopupMode.None;
    private string pendingLobbyName = string.Empty;

    private void Start()
    {
        InitializeButtons();
        InitializeNetworkCallbacks();
        SetInitialUIState();
        RefreshLobbyList();
        StartCoroutine(AutoRefreshLobbyList());
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
        backButton.onClick.AddListener(OnBackToMenu);
        startHostButton.onClick.AddListener(StartHost);
        startGameButton.onClick.AddListener(OnStartGameClicked);
        exitButton.onClick.AddListener(OnExitClicked);
        joinByIdButton.onClick.AddListener(() => ShowJoinNamePopup(null, "EnterID"));
        confirmNameButton.onClick.AddListener(OnConfirmName);
        closeEnterNamePanelButton.onClick.AddListener(CloseNamePopup);
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
        lobbyPanel.SetActive(false);
        enterNamePopup.SetActive(false);
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnServerStarted -= OnServerStarted;
        }
    }

    private void StartHost()
    {
        isCreatingLobby = true;
        currentPopupMode = NamePopupMode.EnterLobbyName;
        ShowNamePopup("Enter lobby name to start");

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



    public void ShowJoinNamePopup(LobbyInfo lobby, string mode = "")
    {
        pendingLobbyToJoin = lobby;

        if (isCreatingLobby && currentPopupMode == NamePopupMode.EnterLobbyName)
        {
            ShowNamePopup("Enter lobby name to start");
        }
        else if (isCreatingLobby && currentPopupMode == NamePopupMode.EnterPlayerName)
        {
            ShowNamePopup("Enter your name");
        }
        else if (lobby != null)
        {
            currentPopupMode = NamePopupMode.JoinLobby;
            ShowNamePopup($"Enter name to join {lobby.lobbyName}");
        }
        else if (mode == "EnterID")
        {
            currentPopupMode = NamePopupMode.EnterLobbyId;
            ShowNamePopup("Enter lobby ID");
        }
    }

    private void ShowNamePopup(string title)
    {
        popupTitleText?.SetText(title);
        nameInputField.text = string.Empty;
        enterNamePopup.SetActive(true);
    }
    private void OnExitClicked()
    {
        HandleExitLogic();
    }

    public void HandleExitLogic()
    {
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
        localUserName = string.Empty;
        pendingLobbyToJoin = null;
    }

    private void ResetUIState()
    {
        lobbyPanel.SetActive(false);
        enterNamePopup.SetActive(false);
        mainPanel.SetActive(true);
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
                localUserName = "Host";
                ShowLobbyUI(lobby);
                heartbeatRoutine = StartCoroutine(SendHeartbeatRoutine());
                StartCoroutine(PollLobbyInfo());
            }
            else
            {
                Debug.LogWarning("⚠️ Could not reach lobby server, starting local offline host...");

                currentLobbyId = "LOCAL";
                localUserName = "Host";
                isCreatingLobby = false;
                enterNamePopup.SetActive(false);
                mainPanel.SetActive(false);

                ShowOfflineLobbyUI();
            }
        }
    }

    private void ShowOfflineLobbyUI()
    {
        lobbyPanel.SetActive(true);
        lobbyId.text = "Offline Mode";
        startGameButton.interactable = true;

        ClearContainer(playerListContainer);
        var item = Instantiate(playerItemPrefab, playerListContainer);
        var comp = item.GetComponent<PlayerItem>();
        if (comp != null)
        {
            comp.Setup(localUserName, "LOCAL_ID", NetworkManager.Singleton.LocalClientId, this, false);
        }
    }


    private void ShowLobbyUI(LobbyInfo lobby)
    {
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
                bool canKick = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
                comp.Setup(user.userName, user.userId, user.clientId, this, canKick);
            }
        }
        if (gameplayCanvas != null)
        {
            gameplayCanvas.UpdateExitButtonState(lobby);
        }
    }
    private void CloseNamePopup()
    {
        enterNamePopup.SetActive(false);
        ResetPopupState();
    }

    private void ResetPopupState()
    {
        currentPopupMode = NamePopupMode.None;
        pendingLobbyName = string.Empty;
        pendingLobbyToJoin = null;
        isCreatingLobby = false;
    }
    private void OnConfirmName()
    {
        string input = nameInputField.text.Trim();
        if (string.IsNullOrEmpty(input)) return;

        if (input.Length > 8)
        {
            UIManager.Instance?.SendNotification("Name cannot be longer than 8 characters");
            UIManager.Instance?.OpenNotification();
            return;
        }

        if (!Regex.IsMatch(input, @"^[a-zA-Z0-9]+$"))
        {
            UIManager.Instance?.SendNotification("Name cannot contain special characters or accents");
            UIManager.Instance?.OpenNotification();
            return;
        }
        switch (currentPopupMode)
        {
            case NamePopupMode.EnterLobbyName:
                pendingLobbyName = input;
                currentPopupMode = NamePopupMode.EnterPlayerName;
                ShowNamePopup("Enter your name");
                break;

            case NamePopupMode.EnterPlayerName:
                localUserName = input;
                enterNamePopup.SetActive(false);
                SavePlayerPrefs(localUserName);
                isCreatingLobby = false;
                mainPanel.SetActive(false);
                StartCoroutine(StartHostRoutine(localUserName, pendingLobbyName));
                // RESET
                pendingLobbyName = "";
                currentPopupMode = NamePopupMode.None;
                break;

            case NamePopupMode.EnterLobbyId:
                pendingLobbyToJoin = new LobbyInfo { lobbyId = input };
                currentPopupMode = NamePopupMode.JoinLobby;
                ShowNamePopup("Enter your name");
                break;

            case NamePopupMode.JoinLobby:
                localUserName = input;
                enterNamePopup.SetActive(false);
                SavePlayerPrefs(localUserName);
                if (pendingLobbyToJoin != null)
                {
                    StartCoroutine(JoinLobbyRoutine(pendingLobbyToJoin.lobbyId, localUserName));
                }
                // RESET
                pendingLobbyToJoin = null;
                currentPopupMode = NamePopupMode.None;
                break;
        }
    }

    private void SavePlayerPrefs(string playerName)
    {
        string playerId = System.Guid.NewGuid().ToString();
        PlayerPrefs.SetString("PlayerId", playerId);
        PlayerPrefs.SetString("PlayerName", playerName);
        PlayerPrefs.Save();
    }

    private IEnumerator StartHostRoutine(string hostName, string lobbyName)
    {
        ushort port = 7777;
        string ipLan = GetHostLANIP();
        pendingLobbyToJoin = null;
        transport.SetConnectionData("0.0.0.0", port);

        if (!networkManager.StartHost())
        {
            UIManager.Instance?.SendNotification("Failed to start hosting. Please try again.");
            UIManager.Instance?.OpenNotification();
            isCreatingLobby = false;
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
            hostName = hostName
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
            isCreatingLobby = false;
            enterNamePopup.SetActive(false);
            mainPanel.SetActive(false);
            ShowOfflineLobbyUI();
        }
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

        string playerId = System.Guid.NewGuid().ToString();
        PlayerPrefs.SetString("PlayerId", playerId);

        var user = new UserInfo { userId = playerId, userName = playerName };
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
            StartCoroutine(PollLobbyInfo());
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
        HandleExitLogic();
        lobbyPanel.SetActive(false);
        enterNamePopup.SetActive(false);
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

    public void KickPlayer(string userId, ulong clientId)
    {
        if (string.IsNullOrEmpty(currentLobbyId)) return;
        StartCoroutine(KickAndNotifyRoutine(currentLobbyId, userId, clientId));
    }

    private IEnumerator KickAndNotifyRoutine(string lobbyId, string userId, ulong clientId)
    {
        if (NetworkManager.Singleton != null && clientId == NetworkManager.Singleton.LocalClientId)
        {
            yield break;
        }
        string url = $"{SERVER_URL}/{lobbyId}/kick/{userId}";
        using (var www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(new byte[0]);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                if (NetworkManager.Singleton.IsHost)
                {
                    RequestKickServerRpc(clientId);
                }
                StartCoroutine(PollLobbyInfo());
            }
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

    [ServerRpc(RequireOwnership = false)]
    public void RequestKickServerRpc(ulong clientId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId)
            {
                return;
            }

            if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId))
            {
                return;
            }

            try
            {
                var targetClient = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { clientId }
                    }
                };

                KickClientClientRpc(targetClient);
                NetworkManager.Singleton.DisconnectClient(clientId);
                CleanupPlayerObjects(clientId);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error kicking client {clientId}: {e.Message}");
            }
        }
    }

    [ClientRpc]
    private void KickClientClientRpc(ClientRpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsClient &&
            !NetworkManager.Singleton.IsHost)
        {
            HandleKickedClient();
        }
    }

    private void HandleKickedClient()
    {
        string lobbyId = currentLobbyId;
        string playerId = PlayerPrefs.GetString("PlayerId", "");
        string userName = localUserName;

        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        if (!string.IsNullOrEmpty(lobbyId) && !string.IsNullOrEmpty(playerId))
        {
            var user = new UserInfo { userId = playerId, userName = userName };
            StartCoroutine(LeaveLobbyRoutine(lobbyId, user));
        }

        ResetState();
        ResetUIState();
        RefreshLobbyList();
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
