using DG.Tweening;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NotificationCanvas : BaseCanvas
{
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [SerializeField] private TextMeshProUGUI toastText;
    [SerializeField] private float toastDuration = 0.5f;
    [SerializeField] private float toastFadeTime = 0.25f;
    [Header("Countdown Toast")]
    [SerializeField] private TextMeshProUGUI countdownToastText;
    private Action<bool> onDecision;

    private Tween currentToastTween;

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
        if (toastText == null) return;

        currentToastTween?.Kill();

        toastText.gameObject.SetActive(true);
        toastText.text = message;

        Color c = toastText.color;
        c.a = 0f;
        toastText.color = c;

        Vector3 originalPos = toastText.rectTransform.anchoredPosition;
        toastText.rectTransform.anchoredPosition = originalPos + new Vector3(0, -50f, 0);

        Sequence seq = DOTween.Sequence();

        seq.Append(toastText.DOFade(1f, toastFadeTime)); 
        seq.Join(toastText.rectTransform.DOAnchorPos(originalPos, toastFadeTime).SetEase(Ease.OutBack));

        seq.AppendInterval(toastDuration); 
        seq.Append(toastText.DOFade(0f, toastFadeTime));
        seq.Join(toastText.rectTransform.DOAnchorPos(originalPos + new Vector3(0, 50f, 0), toastFadeTime).SetEase(Ease.InBack));

        seq.OnComplete(() => toastText.gameObject.SetActive(false));

        currentToastTween = seq;
    }

}