using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
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
    [SerializeField] private TextMeshProUGUI hostStatusText;

    [Header("Join Panel References")]
    [SerializeField] private Button startJoinButton;
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private TextMeshProUGUI joinStatusText;

    [Header("Network")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport transport;

    private void Start()
    {
        openHostPanelButton.onClick.AddListener(() => TogglePanels(true));
        openJoinPanelButton.onClick.AddListener(() => TogglePanels(false));
        backButton.onClick.AddListener(OnBackToMenu);

        startHostButton.onClick.AddListener(StartHost);

        startJoinButton.onClick.AddListener(StartClient);

        networkManager.OnClientConnectedCallback += OnClientConnected;
        networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        networkManager.OnServerStarted += OnServerStarted;
    }

    private void TogglePanels(bool showHost)
    {
        hostPanel.SetActive(showHost);
        joinPanel.SetActive(!showHost);
    }

    private void StartHost()
    {
        try
        {
            transport.SetConnectionData("0.0.0.0", 7777);
            networkManager.StartHost();
            hostStatusText.text = "Đang khởi động Host...";
        }
        catch (System.Exception e)
        {
            hostStatusText.text = $"Lỗi Host: {e.Message}";
        }
    }

    private void StartClient()
    {
        try
        {
            if (!string.IsNullOrEmpty(ipInputField.text))
            {
                transport.SetConnectionData(ipInputField.text, 7777);
            }

            networkManager.StartClient();
            joinStatusText.text = $"Đang kết nối đến {ipInputField.text}...";
        }
        catch (System.Exception e)
        {
            joinStatusText.text = $"Lỗi kết nối: {e.Message}";
        }
    }

    private void OnServerStarted()
    {
        hostStatusText.text = "Host đã khởi động thành công!";
    }

    private void OnClientConnected(ulong clientId)
    {
        if (networkManager.IsHost)
            hostStatusText.text = $"Client {clientId} đã kết nối";
        else
            joinStatusText.text = "Đã kết nối thành công đến Host!";
    }

    private void OnClientDisconnected(ulong clientId)
    {
        hostStatusText.text = "Đã ngắt kết nối";
        joinStatusText.text = "Đã ngắt kết nối";
    }

    private void OnBackToMenu()
    {
        hostPanel.SetActive(false);
        joinPanel.SetActive(false);
        UIManager.Instance.CloseNetwork();
        UIManager.Instance.OpenMainMenu();
    }
    private void OnEnable()
    {
        hostPanel.SetActive(true);
        joinPanel.SetActive(false);
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
}
