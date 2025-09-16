using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class ConnectionCanvas : BaseCanvas
{
    [Header("UI Panels")]
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject enterNamePopup;
    [SerializeField] private GamePlayCanvas gameplayCanvas;

    [Header("Main Panel")]
    [SerializeField] private Button startHostButton;
    [SerializeField] private Button refreshListButton;
    [SerializeField] private Button joinByIdButton;
    [SerializeField] private Transform lobbyListContainer;
    [SerializeField] private GameObject lobbyItemPrefab;
    [SerializeField] private TMP_InputField lobbyIdInputField;

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

    private const string SERVER_URL = "http://192.168.1.32:5000/api/lobby";
    private string currentLobbyId = string.Empty;
    private string localUserName = string.Empty;
    private LobbyInfo pendingLobbyToJoin = null;
    private Coroutine heartbeatRoutine;

    private void Start()
    {
        InitializeButtons();
        InitializeNetworkCallbacks();
        SetInitialUIState();
        RefreshLobbyList();
    }

    private void InitializeButtons()
    {
        backButton.onClick.AddListener(OnBackToMenu);
        startHostButton.onClick.AddListener(StartHost);
        startGameButton.onClick.AddListener(OnStartGameClicked);
        exitButton.onClick.AddListener(OnExitClicked);
        refreshListButton.onClick.AddListener(RefreshLobbyList);
        joinByIdButton.onClick.AddListener(() => ShowJoinNamePopup(null, "Enter"));
        confirmNameButton.onClick.AddListener(OnConfirmName);
        closeEnterNamePanelButton.onClick.AddListener(() => enterNamePopup.SetActive(false));
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

    // === HOST FUNCTIONS ===
    private void StartHost()
    {
        if (networkManager.IsListening) 
            networkManager.Shutdown();
        StopAllCoroutines();
        StartCoroutine(StartHostRoutine());
        ObjectPool.Instance.RebuildPool();
        mainPanel.SetActive(false);
    }

    private void OnStartGameClicked()
    {
        if (networkManager.IsHost)
        {
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
        Debug.Log("Handling exit logic...");
        if (networkManager == null) return;

        bool wasHost = networkManager.IsHost;
        bool wasClient = networkManager.IsClient;
        string playerId = PlayerPrefs.GetString("PlayerId", "");
        string lobbyId = currentLobbyId;

        if (networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        // Nếu là host → dừng heartbeat + poll
        if (wasHost)
        {
            if (heartbeatRoutine != null) StopCoroutine(heartbeatRoutine);

            if (!string.IsNullOrEmpty(lobbyId))
            {
                StartCoroutine(DeleteLobbyRoutine(lobbyId));
            }
        }
        // Nếu là client → chỉ dừng poll khi rời lobby thành công
        else if (wasClient)
        {
            if (!string.IsNullOrEmpty(lobbyId) && !string.IsNullOrEmpty(playerId))
            {
                var user = new UserInfo { userId = playerId, userName = localUserName };
                StartCoroutine(LeaveLobbyRoutine(lobbyId, user));
            }
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

    private IEnumerator StartHostRoutine()
    {
        ushort port = 7777;
        string ipLan = GetHostLANIP();

        transport.SetConnectionData("0.0.0.0", port);

        if (!networkManager.StartHost())
        {
            yield break;
        }

        string hostId = System.Guid.NewGuid().ToString();
        PlayerPrefs.SetString("PlayerId", hostId);

        var request = new LobbyRegistrationRequest
        {
            lobbyName = string.IsNullOrEmpty(lobbyIdInputField.text) ? "Lobby" : lobbyIdInputField.text,
            hostIpAddress = ipLan,
            hostPort = port,
            maxPlayers = 6,
            hostName = "Host"
        };

        yield return StartCoroutine(RegisterLobbyOnServer(request));
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
                Debug.LogError($"Register lobby failed: {www.error}");
            }
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
    }


    // === JOIN FUNCTIONS ===
    public void ShowJoinNamePopup(LobbyInfo lobby, string customTitle = null)
    {
        pendingLobbyToJoin = lobby;
        string title = customTitle ?? $"Tham gia: {lobby.lobbyName} ({lobby.currentPlayers}/{lobby.maxPlayers})";
        
        popupTitleText?.SetText(title);
        nameInputField.text = string.Empty;
        enterNamePopup.SetActive(true);
    }

    private void OnConfirmName()
    {
        string playerName = nameInputField.text.Trim();
        if (string.IsNullOrEmpty(playerName)) return;

        localUserName = playerName;
        enterNamePopup.SetActive(false);

        string lobbyId = pendingLobbyToJoin?.lobbyId ?? lobbyIdInputField.text.Trim();
        if (!string.IsNullOrEmpty(lobbyId))
        {
            StartCoroutine(JoinLobbyRoutine(lobbyId, playerName));
        }
    }

    private IEnumerator JoinLobbyRoutine(string lobbyId, string playerName)
    {
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
                RefreshLobbyList();
            }
            else
            {
                Debug.LogError($"Join lobby failed: {www.error}");
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

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Leave lobby failed: {www.error}");
            }
        }
    }

    public void JoinLobby(string ip, int port)
    {
        transport.SetConnectionData(ip, (ushort)port);
        networkManager.StartClient();
    }

    // === REFRESH LOBBY LIST ===
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

    // === NETWORK CALLBACKS ===
    private void OnServerStarted()
    {
        // Server started callback
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
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("Cleanup skipped: NetworkManager is null or not server");
            return;
        }

        if (NetworkManager.Singleton.SpawnManager == null)
        {
            Debug.LogWarning("Cleanup skipped: SpawnManager is null");
            return;
        }

        foreach (var netObj in NetworkManager.Singleton.SpawnManager.SpawnedObjectsList)
        {
            if (netObj != null && netObj.OwnerClientId == clientId)
            {
                netObj.gameObject.SetActive(false);
                Debug.Log($"[Host] Cleanup object {netObj.name} for client {clientId}");
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

    // === UTILITY FUNCTIONS ===
    private void OnBackToMenu()
    {
        HandleExitLogic();
        lobbyPanel.SetActive(false);
        enterNamePopup.SetActive(false);
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
            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"Lobby {lobbyId} deleted");
            }
        }
    }

    public void KickPlayer(string userId, ulong clientId)
    {
        if (string.IsNullOrEmpty(currentLobbyId)) return;
        StartCoroutine(KickAndNotifyRoutine(currentLobbyId, userId, clientId));
    }

    private IEnumerator KickAndNotifyRoutine(string lobbyId, string userId, ulong clientId)
    {
        string url = $"{SERVER_URL}/{lobbyId}/kick/{userId}";
        using (var www = new UnityWebRequest(url, "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(new byte[0]);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"Kicked player {userId} on server");

                // gọi Netcode kick client
                RequestKickServerRpc(clientId);

                // cập nhật lại lobby host
                StartCoroutine(PollLobbyInfo());
            }
            else
            {
                Debug.LogError($"Kick player failed: {www.error} {www.downloadHandler.text} → URL: {url}");
            }
        }
    }



    private IEnumerator SendHeartbeatRoutine()
    {
        while (networkManager != null && networkManager.IsHost && !networkManager.ShutdownInProgress)
        {
            using (var www = UnityWebRequest.PostWwwForm($"{SERVER_URL}/{currentLobbyId}/heartbeat", ""))
            {
                yield return www.SendWebRequest();
                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("Heartbeat failed: " + www.error);
                }
            }
            yield return new WaitForSeconds(5f); // ping mỗi 5s
        }
    }


    [ServerRpc(RequireOwnership = false)]
    public void RequestKickServerRpc(ulong clientId)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            var targetClient = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };

            KickClientClientRpc(targetClient);

            // cắt kết nối client đó
            NetworkManager.Singleton.DisconnectClient(clientId);
        }
    }

    [ClientRpc]
    private void KickClientClientRpc(ClientRpcParams rpcParams = default)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && 
        !NetworkManager.Singleton.IsHost)
    {
        Debug.Log("You have been kicked by host");

        // Thoát client
        NetworkManager.Singleton.Shutdown();

        // Quay về main panel
        ResetState();
        ResetUIState();
        RefreshLobbyList();
    }
    }



}

// === MODELS ===
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

// === JSON HELPER ===
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