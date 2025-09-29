using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NotificationCanvas : BaseCanvas
{
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI messageText;
    void Start()
    {
        closeButton.onClick.AddListener(CloseNotificationCanvas);
    }

    public void SetText(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
    }
    public void HideCloseButton()
    {
        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(false);
        }
    }

    public void ShowCloseButton()
    {
        if (closeButton != null) 
        {
            closeButton.gameObject.SetActive(true); 
        }
    }

    private void CloseNotificationCanvas()
    {
        Hide();
    }
}
