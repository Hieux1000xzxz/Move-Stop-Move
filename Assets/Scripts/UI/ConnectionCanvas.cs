using System;
using System.Text;
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
    [SerializeField] private Button openHostPanelButton;
    [SerializeField] private Button openJoinPanelButton;
    [SerializeField] private Button backButton;

    [Header("Panels")]
    [SerializeField] private GameObject hostPanel;
    [SerializeField] private GameObject joinPanel;
    [SerializeField] private GameObject lobbyPanel;

    [Header("Host Panel References")]
    [SerializeField] private Button startHostButton;
    [SerializeField] private TMP_InputField lobbyNameInputField;
    [SerializeField] private TextMeshProUGUI hostStatusText;

    [Header("Join Panel References")]
    [SerializeField] private Button refreshListButton;
    [SerializeField] private Button joinByIdButton;
    [SerializeField] private TextMeshProUGUI joinStatusText;
    [SerializeField] private Transform lobbyListContainer;
    [SerializeField] private GameObject lobbyItemPrefab;
    [SerializeField] private TMP_InputField lobbyIdInputField;

    [Header("Lobby Panel References")]
    [SerializeField] private Transform playerListContainer;
    [SerializeField] private GameObject playerItemPrefab;
    [SerializeField] private Button startGameButton;

    [Header("Enter Name Popup")]
    [SerializeField] private GameObject enterNamePopup;
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button confirmNameButton;

    [Header("Network")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport transport;

    private const string SERVER_URL = "http://192.168.1.32:5000/api/lobby";
    private string currentLobbyId = string.Empty;
    private string localUserName = string.Empty;

    private void Start()
    {
        // Menu buttons
        openHostPanelButton.onClick.AddListener(() => TogglePanels(true));
        openJoinPanelButton.onClick.AddListener(() => TogglePanels(false));
        backButton.onClick.AddListener(OnBackToMenu);

        // Host actions
        startHostButton.onClick.AddListener(StartHost);

        // Join actions
        refreshListButton.onClick.AddListener(RefreshLobbyList);
        joinByIdButton.onClick.AddListener(() => enterNamePopup.SetActive(true));

        confirmNameButton.onClick.AddListener(OnConfirmName);

        // Network callbacks
        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        networkManager.OnServerStarted += OnServerStarted;

        lobbyPanel.SetActive(false);
        enterNamePopup.SetActive(false);
    }

    private void TogglePanels(bool showHost)
    {
        hostPanel.SetActive(showHost);
        joinPanel.SetActive(!showHost);
    }

    // === HOST ===
    private async void StartHost()
    {
        try
        {
            ushort port = 7777;
            string ipLan = GetHostLANIP();

            transport.SetConnectionData("0.0.0.0", port);

            if (networkManager.StartHost())
            {
                hostStatusText.text = $"Đang khởi động Host ({ipLan}:{port})...";

                var request = new LobbyRegistrationRequest
                {
                    lobbyName = string.IsNullOrEmpty(lobbyNameInputField.text) ? "Lobby" : lobbyNameInputField.text,
                    hostIpAddress = ipLan,
                    hostPort = port,
                    maxPlayers = 6
                };

                string json = JsonUtility.ToJson(request);

                using (var www = new UnityWebRequest($"{SERVER_URL}/register", "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                    www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    www.downloadHandler = new DownloadHandlerBuffer();
                    www.SetRequestHeader("Content-Type", "application/json");

                    await www.SendWebRequest();

                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        var lobby = JsonUtility.FromJson<LobbyInfo>(www.downloadHandler.text);
                        currentLobbyId = lobby.lobbyId;
                        ShowLobbyUI(lobby);
                        hostStatusText.text = $"Phòng đã tạo tại {ipLan}:{port} (ID: {currentLobbyId})";
                    }
                    else
                    {
                        hostStatusText.text = $"Lỗi đăng ký lobby: {www.error}";
                    }
                }
            }
        }
        catch (Exception e)
        {
            hostStatusText.text = $"Lỗi Host: {e.Message}";
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
        foreach (Transform child in playerListContainer)
            Destroy(child.gameObject);

        foreach (var user in lobby.users)
        {
            var item = Instantiate(playerItemPrefab, playerListContainer);
            item.GetComponent<PlayerItem>().Setup(user.userName);
        }
    }

    // === JOIN ===
    private void OnConfirmName()
    {
        string playerName = nameInputField.text.Trim();
        if (!string.IsNullOrEmpty(playerName))
        {
            localUserName = playerName;
            enterNamePopup.SetActive(false);
            JoinByLobbyIdWithName(playerName);
        }
    }

    public async void JoinByLobbyIdWithName(string playerName)
    {
        string lobbyId = lobbyIdInputField.text.Trim();
        if (string.IsNullOrEmpty(lobbyId)) return;

        var user = new UserInfo
        {
            userId = Guid.NewGuid().ToString(),
            userName = playerName
        };

        string json = JsonUtility.ToJson(user);

        using (var www = new UnityWebRequest($"{SERVER_URL}/{lobbyId}/join", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            await www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string responseJson = www.downloadHandler.text;
                LobbyInfo lobby = JsonUtility.FromJson<LobbyInfo>(responseJson);
                ShowLobbyUI(lobby);
                JoinLobby(lobby.hostIpAddress, lobby.hostPort);
                joinStatusText.text = $"Đang kết nối {lobby.lobbyName} ({lobby.currentPlayers}/{lobby.maxPlayers})...";
            }
            else
            {
                joinStatusText.text = $"Lỗi join phòng: {www.error}";
            }
        }
    }

    public void JoinLobby(string ip, int port)
    {
        transport.SetConnectionData(ip, (ushort)port);
        networkManager.StartClient();
    }

    public async void RefreshLobbyList()
    {
        using (var www = UnityWebRequest.Get($"{SERVER_URL}/list"))
        {
            await www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                string json = www.downloadHandler.text;
                LobbyInfo[] lobbies = JsonHelper.FromJson<LobbyInfo>(json);

                foreach (Transform child in lobbyListContainer)
                    Destroy(child.gameObject);

                foreach (var lobby in lobbies)
                {
                    var item = Instantiate(lobbyItemPrefab, lobbyListContainer);
                    item.GetComponent<LobbyItem>().Setup(lobby, this);
                }
            }
            else
            {
                joinStatusText.text = $"Lỗi tải danh sách: {www.error}";
            }
        }
    }

    // === CALLBACKS ===
    private void OnServerStarted()
    {
        hostStatusText.text = "Host đã khởi động thành công!";
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
            joinStatusText.text = "Đã kết nối thành công!";
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        joinStatusText.text = "Đã ngắt kết nối";
    }

    // === UTIL ===
    private void OnBackToMenu()
    {
        hostPanel.SetActive(false);
        joinPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        enterNamePopup.SetActive(false);
        UIManager.Instance.CloseNetwork();
        UIManager.Instance.OpenMainMenu();
    }

    private string GetHostLANIP()
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
        return "127.0.0.1";
    }

    private void OnDisable()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            networkManager.OnServerStarted -= OnServerStarted;
        }
    }

    private async void DeleteLobby()
    {
        if (string.IsNullOrEmpty(currentLobbyId)) return;

        using (var www = UnityWebRequest.Delete($"{SERVER_URL}/{currentLobbyId}/delete"))
        {
            await www.SendWebRequest();
        }
    }

    // Poll server lobby info every 2 seconds (host view)
    private System.Collections.IEnumerator PollLobbyInfo()
    {
        while (networkManager.IsHost)
        {
            if (!string.IsNullOrEmpty(currentLobbyId))
            {
                using (var www = UnityWebRequest.Get($"{SERVER_URL}/find/{currentLobbyId}"))
                {
                    yield return www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        LobbyInfo lobby = JsonUtility.FromJson<LobbyInfo>(www.downloadHandler.text);
                        UpdatePlayerList(lobby);
                    }
                }
            }
            yield return new WaitForSeconds(2f);
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
