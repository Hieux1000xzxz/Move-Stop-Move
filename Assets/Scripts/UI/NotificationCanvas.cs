using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NotificationCanvas : BaseCanvas
{
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI toastText;
    [SerializeField] private float toastDuration = 0.5f;
    [SerializeField] private float toastFadeTime = 0.25f;

    [Header("Countdown Toast")] [SerializeField]
    private TextMeshProUGUI countdownToastText;

    private Action<bool> onDecision;
    private Tween currentToastTween;
    private Vector3 originalToastPos;

    private void Start()
    {
        if (toastText != null)
            originalToastPos = toastText.rectTransform.anchoredPosition;

        closeButton.onClick.AddListener(() => HandleDecision(false));
        cancelButton.onClick.AddListener(() => HandleDecision(false));
        confirmButton.onClick.AddListener(() => HandleDecision(true));
    }

    private void HandleDecision(bool decision)
    {
        onDecision?.Invoke(decision);
        CloseNotificationCanvas();
    }

    private void OnEnable()
    {
        if (toastText != null)
        {
            toastText.gameObject.SetActive(true);
            originalToastPos = toastText.rectTransform.anchoredPosition;
        }
    }


    public void SetText(string message)
    {
        if (messageText != null)
            messageText.text = message;
    }

    public void HideCloseButton() => closeButton.gameObject.SetActive(false);
    public void ShowCloseButton() => closeButton.gameObject.SetActive(true);

    public void ShowConfirmButton()
    {
        confirmButton.gameObject.SetActive(true);
        cancelButton.gameObject.SetActive(true);
    }

    public void HideConfirmButton()
    {
        confirmButton.gameObject.SetActive(false);
        cancelButton.gameObject.SetActive(false);
    }

    public void ShowMainPanel()
    {
        mainPanel.SetActive(true);
        toastText.gameObject.SetActive(false);
        panel.SetActive(true);
    }

    public void HideMainPanel()
    {
        mainPanel.SetActive(false);
    }

    public void SetCallback(Action<bool> decisionCallback)
    {
        onDecision = decisionCallback;
    }

    private void CloseNotificationCanvas() => Hide();

    public void ShowToast(string message)
    {
        if (toastText == null) return;

        panel.SetActive(false);
        currentToastTween.Kill();

        toastText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        toastText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        toastText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        toastText.rectTransform.anchoredPosition = new Vector2(0f, 150f);
        toastText.gameObject.SetActive(true);
        toastText.text = message;

        var c = toastText.color;
        c.a = 0f;
        toastText.color = c;

        toastText.rectTransform.anchoredPosition += new Vector2(0f, -50f);

        Sequence seq = DOTween.Sequence();
        seq.Append(toastText.DOFade(1f, toastFadeTime));
        seq.Join(toastText.rectTransform.DOAnchorPosY(150f, toastFadeTime).SetEase(Ease.OutBack));
        seq.AppendInterval(toastDuration);
        seq.Append(toastText.DOFade(0f, toastFadeTime));
        seq.Join(toastText.rectTransform.DOAnchorPosY(250f, toastFadeTime).SetEase(Ease.InBack));
        seq.OnComplete(() => { toastText.gameObject.SetActive(false); });

        currentToastTween = seq;
    }
}