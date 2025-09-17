using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private Button kickButton;

    private string userId;
    private ulong clientId;
    private ConnectionCanvas connectionCanvas;

    public void Setup(string playerName, string userId, ulong clientId, ConnectionCanvas canvas, bool canKick)
    {
        this.userId = userId;
        this.clientId = clientId;
        this.connectionCanvas = canvas;

        if (playerNameText != null)
            playerNameText.text = playerName;

        if (kickButton != null)
        {
            kickButton.gameObject.SetActive(canKick);
            kickButton.onClick.RemoveAllListeners();
            kickButton.onClick.AddListener(OnKickClicked);
        }
    }

    private void OnKickClicked()
    {
        if (connectionCanvas != null && !string.IsNullOrEmpty(userId))
        {
            //connectionCanvas.KickPlayer(userId, clientId);
        }
    }
}
