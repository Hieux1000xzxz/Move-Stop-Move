using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class NotificationCanvas : BaseCanvas
{
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("Toast Simple")]
    [SerializeField] private TextMeshProUGUI toastText;
    [Header("Countdown Toast")]
    [SerializeField] private TextMeshProUGUI countdownToastText;
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

        if (toastText != null)
        {
            toastText.gameObject.SetActive(false);
        }
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
    public void ShowMainPanel()
    {
        mainPanel.gameObject.SetActive(true);
        toastText.gameObject.SetActive(false);
    }

    public void HideMainPanel()
    {
        mainPanel.gameObject.SetActive(false);
    }

    public void SetCallback(Action<bool> decisionCallback)
    {
        onDecision = decisionCallback;
    }

    private void CloseNotificationCanvas() => Hide();


    public void ShowToast(string message)
    {
        toastText.gameObject.SetActive(true);
        toastText.text = message;
    }
}