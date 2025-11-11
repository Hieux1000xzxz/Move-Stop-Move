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

    private void OnCloseClicked() => HandleDecision(false);
    private void OnCancelClicked() => HandleDecision(false);
    private void OnConfirmClicked() => HandleDecision(true);

    private void Start()
    {
        if (toastText != null)
            originalToastPos = toastText.rectTransform.anchoredPosition;

        closeButton.onClick.AddListener(OnCloseClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);
        confirmButton.onClick.AddListener(OnConfirmClicked);
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

        ResetToastState(message);
        SetupToastTransform();

        currentToastTween = CreateToastAnimation();
    }

    private void ResetToastState(string message)
    {
        panel.SetActive(false);
        currentToastTween.Kill();

        toastText.gameObject.SetActive(true);
        toastText.text = message;

        var color = toastText.color;
        color.a = 0f;
        toastText.color = color;
    }

    private void SetupToastTransform()
    {
        RectTransform rect = toastText.rectTransform;

        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 150f);

        rect.anchoredPosition += new Vector2(0f, -50f);
    }

    private Tween CreateToastAnimation()
    {
        RectTransform rect = toastText.rectTransform;

        Sequence seq = DOTween.Sequence();
        seq.Append(toastText.DOFade(1f, toastFadeTime));
        seq.Join(rect.DOAnchorPosY(150f, toastFadeTime).SetEase(Ease.OutBack));

        seq.AppendInterval(toastDuration);

        seq.Append(toastText.DOFade(0f, toastFadeTime));
        seq.Join(rect.DOAnchorPosY(250f, toastFadeTime).SetEase(Ease.InBack));

        seq.OnComplete(() => toastText.gameObject.SetActive(false));

        return seq;
    }
}