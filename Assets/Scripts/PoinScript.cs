using UnityEngine;
using DG.Tweening;

public class AttentionPulse : MonoBehaviour
{
    [Header("Pulse Settings")] [SerializeField]
    private float scaleAmount = 1.5f;

    [SerializeField] private float duration = 0.6f;
    [SerializeField] private bool playOnStart = true;

    private Tween pulseTween;

    private void Start()
    {
        if (playOnStart)
            StartPulse();
    }

    public void StartPulse()
    {
        StopPulse();

        Vector3 originalScale = transform.localScale;

        pulseTween = transform.DOScale(originalScale * scaleAmount, duration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    public void StopPulse()
    {
        if (pulseTween != null && pulseTween.IsActive())
            pulseTween.Kill();
        transform.localScale = Vector3.one;
    }
}