using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

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
            joinButton.interactable = false;
            StartCoroutine(DelayJoin());
        }
    }

    private IEnumerator DelayJoin()
    {
        yield return new WaitForSeconds(0.5f);
        connectionCanvas.JoinLobbyDirect(lobbyInfo);
    }

    private void OnDestroy()
    {
        joinButton.onClick.RemoveAllListeners();
    }
}