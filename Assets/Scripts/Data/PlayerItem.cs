using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private Button kickButton;   // nút kick

    private string userId;                        // lưu userId để gọi kick
    private ConnectionCanvas connectionCanvas;    // tham chiếu canvas để gọi KickPlayer

    /// <summary>
    /// Cài đặt tên và id người chơi hiển thị trên UI
    /// </summary>
    public void Setup(string playerName, string userId, ConnectionCanvas canvas, bool canKick)
    {
        this.userId = userId;
        this.connectionCanvas = canvas;

        if (playerNameText != null)
            playerNameText.text = playerName;

        if (kickButton != null)
        {
            kickButton.gameObject.SetActive(canKick); // chỉ hiện khi host
            kickButton.onClick.RemoveAllListeners();
            kickButton.onClick.AddListener(OnKickClicked);
        }
    }

    private void OnKickClicked()
    {
        if (connectionCanvas != null && !string.IsNullOrEmpty(userId))
        {
            connectionCanvas.KickPlayer(userId);
        }
    }
}
