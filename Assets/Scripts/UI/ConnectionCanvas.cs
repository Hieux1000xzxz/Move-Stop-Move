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
    [Header("Menu Buttons")]
    [SerializeField] private Button backButton;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject mainPanel;



    [Header("Main Panel References")]
    [SerializeField] private Button startHostButton;
    [SerializeField] private Button refreshListButton;
    [SerializeField] private Button joinByIdButton;
     [SerializeField] private Transform lobbyListContainer;
    [SerializeField] private GameObject lobbyItemPrefab;
    [SerializeField] private TMP_InputField lobbyIdInputField;

    [Header("Lobby Panel References")]
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerItemPrefab;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button exitButton;

    [Header("Enter Name Popup")]
    [SerializeField] private GameObject enterNamePopup;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button confirmNameButton;
    [SerializeField] private TextMeshProUGUI popupTitleText;

    [Header("Network")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport transport;

    private const string SERVER_URL = "http://192.168.0.91:5000/api/lobby";
    private string currentLobbyId = string.Empty;
    private string localUserName = string.Empty;

    // pending lobby chosen from LobbyItem (when user clicked Join)
    private LobbyInfo pendingLobbyToJoin = null;

    private void Start()
    {

        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(OnBackToMenu);

        // Host actions
        startHostButton.onClick.RemoveAllListeners();
        startHostButton.onClick.AddListener(StartHost);
        startGameButton.onClick.RemoveAllListeners();
        startGameButton.onClick.AddListener(OnStartGameClicked);
        exitButton.onClick.RemoveAllListeners();
        exitButton.onClick.AddListener(OnExitClicked);


        // Join actions
        refreshListButton.onClick.RemoveAllListeners();
        refreshListButton.onClick.AddListener(RefreshLobbyList);

        joinByIdButton.onClick.RemoveAllListeners();
        joinByIdButton.onClick.AddListener(() => ShowJoinNamePopupForId());

        confirmNameButton.onClick.RemoveAllListeners();
        confirmNameButton.onClick.AddListener(OnConfirmName);

        // network callbacks
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
            networkManager.OnServerStarted += OnServerStarted;
        }
     
        lobbyPanel.SetActive(false);
        enterNamePopup.SetActive(false);

        // initial load
        RefreshLobbyList();
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

    // === HOST ===
    private void StartHost()
    {
        StartCoroutine(StartHostRoutine());
                mainPanel.SetActive(false);

    }

    private void OnStartGameClicked()
    {
        if (networkManager.IsHost)
        {
            GameManager.Instance.StartGame();
            GameManager.Instance.StartGameClientRpc();
        }
    }
    private void OnExitClicked()
    {
        if (networkManager == null) return;

        StopAllCoroutines(); // dừng PollLobbyInfo hoặc join coroutine đang chạy

        if (networkManager.IsHost)
        {
            // Host → hủy phòng
            DeleteLobbyLocal();

            // Shutdown host
            if (networkManager.IsListening)
                networkManager.Shutdown();
        }
        else if (networkManager.IsClient)
        {
            // Client → thoát phòng
            string playerId = PlayerPrefs.GetString("PlayerId", "");
            if (!string.IsNullOrEmpty(currentLobbyId) && !string.IsNullOrEmpty(playerId))
            {
                StartCoroutine(LeaveLobbyRoutine(currentLobbyId, playerId));
            }

            if (networkManager.IsClient)
                networkManager.Shutdown();
        }

        // Reset state
        currentLobbyId = string.Empty;
        localUserName = string.Empty;
        pendingLobbyToJoin = null;

        // Reset UI về Join Panel
        lobbyPanel.SetActive(false);
        enterNamePopup.SetActive(false);
        mainPanel.SetActive(true);

        // Làm mới danh sách lobby để có thể join/tạo lại
        RefreshLobbyList();
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


        var request = new LobbyRegistrationRequest
        {
            lobbyName = string.IsNullOrEmpty(lobbyIdInputField.text) ? "Lobby" : lobbyIdInputField.text,
            hostIpAddress = ipLan,
            hostPort = port,
            maxPlayers = 6,
            hostName = "Host" // nếu muốn, có thể lấy tên từ UI
        };

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
                ShowLobbyUI(lobby);
                // start polling lobby info (host)
                StartCoroutine(PollLobbyInfo());
            }
            else
            {
            }
        }
    }

    private void ShowLobbyUI(LobbyInfo lobby)
    {
        lobbyPanel.SetActive(true);
        UpdatePlayerList(lobby);
        startGameButton.interactable = networkManager.IsHost;
    }

    private void UpdatePlayerList(LobbyInfo lobby)
    {
        // clear existing
        for (int i = playerListContainer.childCount - 1; i >= 0; i--)
            Destroy(playerListContainer.GetChild(i).gameObject);

        if (lobby?.users == null) return;

        foreach (var user in lobby.users)
        {
            var item = Instantiate(playerItemPrefab, playerListContainer);
            var comp = item.GetComponent<PlayerItem>();
            if (comp != null) comp.Setup(user.userName);
        }
    }

    // === JOIN FLOW ===

    // Show join popup for a specific LobbyInfo (from LobbyItem)
    public void ShowJoinNamePopup(LobbyInfo lobby)
    {
        pendingLobbyToJoin = lobby;
        ShowJoinPopupCommon($"Tham gia: {lobby.lobbyName} ({lobby.currentPlayers}/{lobby.maxPlayers})");
    }

    // Show join popup when user enters an ID manually (Join by ID button)
    private void ShowJoinNamePopupForId()
    {
        pendingLobbyToJoin = null; // will use lobbyIdInputField
        ShowJoinPopupCommon("Nhập tên để tham gia phòng (theo ID)");
    }

    private void ShowJoinPopupCommon(string title)
    {
        popupTitleText?.SetText(title);
        nameInputField.text = string.Empty;
        enterNamePopup.SetActive(true);
    }

    private void OnConfirmName()
    {
        string playerName = nameInputField.text.Trim();
        if (string.IsNullOrEmpty(playerName))
        {
            return;
        }

        localUserName = playerName;
        enterNamePopup.SetActive(false);

        if (pendingLobbyToJoin != null)
        {
            // join by chosen lobby
            StartCoroutine(JoinLobbyRoutine(pendingLobbyToJoin.lobbyId, playerName));
        }
        else
        {
            // join by ID entered in input
            string lobbyId = lobbyIdInputField.text.Trim();
            if (string.IsNullOrEmpty(lobbyId))
            {
                return;
            }
            StartCoroutine(JoinLobbyRoutine(lobbyId, playerName));
        }
    }

    private IEnumerator JoinLobbyRoutine(string lobbyId, string playerName)
    {
        var user = new UserInfo { userId = Guid.NewGuid().ToString(), userName = playerName };
        string json = JsonUtility.ToJson(user);

        using (var www = new UnityWebRequest($"{SERVER_URL}/{lobbyId}/join", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string responseJson = www.downloadHandler.text;
                LobbyInfo lobby = JsonUtility.FromJson<LobbyInfo>(responseJson);

                // update UI
                ShowLobbyUI(lobby);

                // connect to host
                JoinLobby(lobby.hostIpAddress, lobby.hostPort);


                // cập nhật danh sách lobby trên view JoinPanel
                RefreshLobbyList();
            }
            else
            {
            }
        }
    }
    private IEnumerator LeaveLobbyRoutine(string lobbyId, string playerId)
    {
        WWWForm form = new WWWForm();
        form.AddField("playerId", playerId);

        using (UnityWebRequest www = UnityWebRequest.Post($"{SERVER_URL}/{lobbyId}/leave", form))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Client] Player {playerId} đã rời phòng {lobbyId}");
            }
            else
            {
                Debug.LogWarning($"[Client] Rời phòng thất bại: {www.error}");
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

                // Clear existing list
                for (int i = lobbyListContainer.childCount - 1; i >= 0; i--)
                    Destroy(lobbyListContainer.GetChild(i).gameObject);

                // Instantiate items
                foreach (var lobby in lobbies)
                {
                    var item = Instantiate(lobbyItemPrefab, lobbyListContainer);
                    var comp = item.GetComponent<LobbyItem>();
                    if (comp != null) comp.Setup(lobby, this);
                }
            }
            else
            {
            }
        }
    }

    // === CALLBACKS ===
    private void OnServerStarted()
    {
    }

    private void OnClientConnected(ulong clientId)
    {
        if (networkManager.IsHost)
        {
            // Host cập nhật số lượng người chơi
            StartCoroutine(PollLobbyInfo());
        }
        else
        {
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!networkManager.IsHost)
        {
            string playerId = PlayerPrefs.GetString("PlayerId", "");
            if (!string.IsNullOrEmpty(currentLobbyId) && !string.IsNullOrEmpty(playerId))
            {
                StartCoroutine(LeaveLobbyRoutine(currentLobbyId, playerId));
            }

            // Reset UI cho client
            lobbyPanel.SetActive(false);
            mainPanel.SetActive(true);
            RefreshLobbyList();
        }
        else
        {
            // host cập nhật danh sách client
            if (!networkManager.ShutdownInProgress)
                StartCoroutine(PollLobbyInfo());
        }
    }


    // === UTIL ===
    private void OnBackToMenu()
    {
        lobbyPanel.SetActive(false);
        enterNamePopup.SetActive(false);

        // If hosting or client, stop network
        //if (networkManager != null)
        //{
        //    if (networkManager.IsHost) networkManager.Shutdown();
        //    else if (networkManager.IsClient) networkManager.Shutdown();
        //}

        UIManager.Instance?.CloseNetwork();
        UIManager.Instance?.OpenMainMenu();
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
            Debug.LogWarning($"Lấy IP LAN thất bại: {e.Message}");
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


    private void OnDisable()
    {
        // ensure listeners removed
        startHostButton.onClick.RemoveListener(StartHost);
        refreshListButton.onClick.RemoveListener(RefreshLobbyList);
        joinByIdButton.onClick.RemoveAllListeners();
        confirmNameButton.onClick.RemoveAllListeners();

        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnServerStarted -= OnServerStarted;
        }
    }

    private void DeleteLobbyLocal()
    {
        // local removal/call delete endpoint if needed
        if (string.IsNullOrEmpty(currentLobbyId)) return;
        StartCoroutine(DeleteLobbyRoutine(currentLobbyId));
    }

    private IEnumerator DeleteLobbyRoutine(string lobbyId)
    {
        using (var www = UnityWebRequest.Delete($"{SERVER_URL}/{lobbyId}/unregister"))
        {
            yield return www.SendWebRequest();
            // ignore response for now
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
    public List<UserInfo> users = new List<UserInfo>();
}

[Serializable]
public class UserInfo
{
    public string userId;
    public string userName;
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