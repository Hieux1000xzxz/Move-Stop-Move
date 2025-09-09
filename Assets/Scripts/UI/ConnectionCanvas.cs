using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

public class ConnectionCanvas : BaseCanvas
{
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport transport;
    [SerializeField] private Button backButton;

    private void Start()
    {
        ipInputField.text = "127.0.0.1";

        hostButton.onClick.AddListener(StartHost);
        joinButton.onClick.AddListener(StartClient);

        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        networkManager.OnServerStarted += OnServerStarted;

        backButton.onClick.AddListener(OnBackToMenu);

    }

    private void StartHost()
    {
        try
        {
            // Lắng nghe trên tất cả interface mạng (LAN/WiFi/Ethernet)
            transport.SetConnectionData("0.0.0.0", 7777);

            networkManager.StartHost();
            statusText.text = "Đang khởi động Host...";
            hostButton.interactable = false;
            joinButton.interactable = false;
        }
        catch (System.Exception e)
        {
            statusText.text = $"Lỗi khởi động Host: {e.Message}";
        }
    }

    private void StartClient()
    {
        try
        {
            if (!string.IsNullOrEmpty(ipInputField.text))
            {
                // Kết nối đến IP host nhập vào
                transport.SetConnectionData(ipInputField.text, 7777);
            }

            networkManager.StartClient();
            statusText.text = $"Đang kết nối đến {ipInputField.text}...";
            hostButton.interactable = false;
            joinButton.interactable = false;
        }
        catch (System.Exception e)
        {
            statusText.text = $"Lỗi kết nối: {e.Message}";
        }
    }

    private void OnServerStarted()
    {
        statusText.text = "Host đã khởi động thành công!";
        Debug.Log("Server started successfully");
    }

    private void OnClientConnected(ulong clientId)
    {
        if (networkManager.IsHost)
        {
            statusText.text = $"Client {clientId} đã kết nối";
            Debug.Log($"Client {clientId} connected to host");
        }
        else
        {
            statusText.text = "Đã kết nối thành công đến Host!";
            Debug.Log("Connected to host successfully");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        statusText.text = "Đã ngắt kết nối";
        hostButton.interactable = true;
        joinButton.interactable = true;
        Debug.Log("Disconnected from server");
    }

    private void OnBackToMenu()
    {
        UIManager.Instance.CloseNetwork();
        UIManager.Instance.OpenMainMenu();
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
}
