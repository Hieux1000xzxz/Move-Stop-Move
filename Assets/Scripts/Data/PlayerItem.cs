using TMPro;
using UnityEngine;

public class PlayerItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;

    /// <summary>
    /// Cài đặt tên người chơi hiển thị trên UI
    /// </summary>
    /// <param name="playerName">Tên người chơi</param>
    public void Setup(string playerName)
    {
        if (playerNameText != null)
            playerNameText.text = playerName;
    }
}
