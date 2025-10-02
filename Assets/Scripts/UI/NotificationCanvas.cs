using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NotificationCanvas : BaseCanvas
{
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private Action<bool> onDecision;

    void Start()
    {
        closeButton.onClick.AddListener(() =>
        {
            onDecision?.Invoke(false);
            CloseNotificationCanvas();
        });

        cancelButton.onClick.AddListener(() =>
        {
            onDecision?.Invoke(false);
            CloseNotificationCanvas();
        });

        confirmButton.onClick.AddListener(() =>
        {
            onDecision?.Invoke(true);
            CloseNotificationCanvas();
        });
    }

    public void SetText(string message)
    {
        if (messageText != null)
            messageText.text = message;
    }

    public void HideCloseButton() => closeButton?.gameObject.SetActive(false);
    public void ShowCloseButton() => closeButton?.gameObject.SetActive(true);

    public void ShowConfirmButton()
    {
        confirmButton?.gameObject.SetActive(true);
        cancelButton?.gameObject.SetActive(true);
    }

    public void HideConfirmButton()
    {
        confirmButton?.gameObject.SetActive(false);
        cancelButton?.gameObject.SetActive(false);
    }

    public void SetCallback(Action<bool> decisionCallback)
    {
        onDecision = decisionCallback;
    }

    private void CloseNotificationCanvas() => Hide();
}
