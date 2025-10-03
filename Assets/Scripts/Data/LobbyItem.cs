using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private Button joinButton;

    private RelayLobbyInfo lobbyInfo;
    private ConnectionCanvas connectionCanvas;

    public void Setup(RelayLobbyInfo info, ConnectionCanvas canvas)
    {
        lobbyInfo = info;
        connectionCanvas = canvas;
        lobbyNameText.text = $"{info.lobbyName} ({info.currentPlayers}/{info.maxPlayers})";

        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(OnJoinClicked);

        joinButton.interactable = info.currentPlayers < info.maxPlayers && !info.isGameStarted;
    }

    private void OnJoinClicked()
    {
        if (connectionCanvas != null && lobbyInfo != null)
        {
            connectionCanvas.JoinLobbyDirect(lobbyInfo);
        }
    }

    private void OnDestroy()
    {
        joinButton.onClick.RemoveAllListeners();
    }
}