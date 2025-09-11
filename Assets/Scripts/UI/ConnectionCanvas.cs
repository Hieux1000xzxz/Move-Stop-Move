using System;
using System.Text;
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

    [Header("Network")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport transport;

    private const string SERVER_URL = "http://192.168.1.32:5000/api/lobby"; // địa chỉ API server của bạn
    private string currentLobbyId = string.Empty;

    private void Start()
    {
        openHostPanelButton.onClick.AddListener(() => TogglePanels(true));
        openJoinPanelButton.onClick.AddListener(() => TogglePanels(false));
        backButton.onClick.AddListener(OnBackToMenu);

        startHostButton.onClick.AddListener(StartHost);
        refreshListButton.onClick.AddListener(RefreshLobbyList);
        joinByIdButton.onClick.AddListener(JoinByLobbyId);

        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        networkManager.OnServerStarted += OnServerStarted;
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
            string ipLan = GetLocalIPAddress();

            // Bind host vào tất cả card mạng
            transport.SetConnectionData("0.0.0.0", port);

            if (networkManager.StartHost())
            {
                hostStatusText.text = $"Đang khởi động Host ({ipLan}:{port})...";

                // Đăng ký lobby lên server
                var request = new LobbyRegistrationRequest
                {
                    lobbyName = string.IsNullOrEmpty(lobbyNameInputField.text)? "Lobby": lobbyNameInputField.text,
                    hostIpAddress = ipLan,   // IP LAN để client join
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

    // === CLIENT JOIN ===
    public async void JoinByLobbyId()
    {
        string lobbyId = lobbyIdInputField.text.Trim();
        if (string.IsNullOrEmpty(lobbyId))
        {
            joinStatusText.text = "Vui lòng nhập ID phòng!";
            return;
        }

        using (var www = UnityWebRequest.Get($"{SERVER_URL}/find/{lobbyId}"))
        {
            await www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string json = www.downloadHandler.text;
                LobbyInfo lobby = JsonUtility.FromJson<LobbyInfo>(json);

                if (lobby != null)
                {
                    currentLobbyId = lobby.lobbyId;
                    JoinLobby(lobby.hostIpAddress, lobby.hostPort);
                    joinStatusText.text = $"Đang kết nối {lobby.lobbyName} ({lobby.currentPlayers}/{lobby.maxPlayers})...";
                }
                else
                {
                    joinStatusText.text = "Không tìm thấy phòng!";
                }
            }
            else
            {
                joinStatusText.text = $"Lỗi tìm phòng: {www.error}";
            }
        }
    }

    public void JoinLobby(string ip, int port)
    {
        transport.SetConnectionData(ip, (ushort)port);
        networkManager.StartClient();
        joinStatusText.text = $"Đang kết nối đến {ip}:{port}...";
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
            int playerCount = networkManager.ConnectedClients.Count;
            UpdatePlayerCount(playerCount);
            hostStatusText.text = $"Client {clientId} đã kết nối ({playerCount}/6)";
        }
        else
        {
            joinStatusText.text = "Đã kết nối thành công!";
            NotifyJoinLobby();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (networkManager.IsHost)
        {
            int playerCount = networkManager.ConnectedClients.Count;
            UpdatePlayerCount(playerCount);
        }
        else
        {
            NotifyLeaveLobby();
        }

        hostStatusText.text = "Đã ngắt kết nối";
        joinStatusText.text = "Đã ngắt kết nối";
    }

    // === API UPDATES ===
    private async void UpdatePlayerCount(int count)
    {
        if (string.IsNullOrEmpty(currentLobbyId)) return;

        var data = new { playerCount = count };
        string json = JsonUtility.ToJson(data);

        using (var www = new UnityWebRequest($"{SERVER_URL}/{currentLobbyId}/updatePlayerCount", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            await www.SendWebRequest();
        }
    }

    private async void NotifyJoinLobby()
    {
        if (string.IsNullOrEmpty(currentLobbyId)) return;

        using (var www = new UnityWebRequest($"{SERVER_URL}/{currentLobbyId}/join", "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(new byte[0]);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            await www.SendWebRequest();
        }
    }

    private async void NotifyLeaveLobby()
    {
        if (string.IsNullOrEmpty(currentLobbyId)) return;

        using (var www = new UnityWebRequest($"{SERVER_URL}/{currentLobbyId}/leave", "POST"))
        {
            www.uploadHandler = new UploadHandlerRaw(new byte[0]);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            await www.SendWebRequest();
        }
    }

    // === UTILS ===
    private void OnBackToMenu()
    {
        hostPanel.SetActive(false);
        joinPanel.SetActive(false);
        UIManager.Instance.CloseNetwork();
        UIManager.Instance.OpenMainMenu();
    }


    private string GetLocalIPAddress()
    {
        var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                if (ip.ToString().StartsWith("192.168.") || ip.ToString().StartsWith("10."))
                    return ip.ToString();
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
