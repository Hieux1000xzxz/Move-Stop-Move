using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private Image avatarImage;
    private string userId;
    private ulong clientId;
    private ConnectionCanvas connectionCanvas;
    private bool canKick;

    public void Setup(string playerName, string userId, ulong clientId, ConnectionCanvas canvas, bool canKick, Sprite avatarSprite)
    {
        this.userId = userId;
        this.clientId = clientId;
        this.connectionCanvas = canvas;
        this.canKick = canKick;

        playerNameText.text = playerName;
        if (avatarImage != null && avatarSprite != null)
        {
            avatarImage.sprite = avatarSprite;
        }
    }
}
