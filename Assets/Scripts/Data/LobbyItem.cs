using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private Button joinButton;

    private string ip;
    private int port;
    private ConnectionCanvas connectionCanvas;

    public void Setup(LobbyInfo info, ConnectionCanvas canvas)
    {
        lobbyNameText.text = $"{info.lobbyName} ({info.currentPlayers}/{info.maxPlayers})";
        ip = info.hostIpAddress;
        port = info.hostPort;
        connectionCanvas = canvas;
        joinButton.onClick.AddListener(OnJoinClicked);
    }

    private void OnJoinClicked()
    {
        connectionCanvas.JoinLobby(ip, port);
    }
}
