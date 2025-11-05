using UnityEngine;
using DG.Tweening;

public class FloatPulseEffect : MonoBehaviour
{
    [Header("Target Object")]
    [SerializeField] private Transform target;

    [Header("Scale Settings")]
    [SerializeField] private float scaleAmount = 1.1f;  
    [SerializeField] private float scaleDuration = 0.6f;

    [Header("Float Settings")]
    [SerializeField] private float floatDistance = 15f; 
    [SerializeField] private float floatDuration = 1.2f;

    private Tween scaleTween;
    private Tween floatTween;

    private void Start()
    {
        if (target == null)
            target = transform;

        StartPulseAndFloat();
    }

    public void StartPulseAndFloat()
    {
        scaleTween = target.DOScale(scaleAmount, scaleDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);

        floatTween = target.DOLocalMoveY(target.localPosition.y + floatDistance, floatDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }
    
}