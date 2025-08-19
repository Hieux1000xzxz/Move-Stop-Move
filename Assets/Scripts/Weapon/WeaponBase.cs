using UnityEngine;
using System.Collections;
using DG.Tweening;

public abstract class WeaponBase : MonoBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] protected float speed = 10f;
    [SerializeField] protected float lifetime = 5f;
    [SerializeField] protected int damage = 10;
    [SerializeField] public float attackDelay = 0.2f;
    [SerializeField] protected Rigidbody rb;

    [Header("Rotation Settings")]
    [SerializeField] protected bool enableRotation = true;
    [SerializeField] protected float rotationSpeed = 360f;
    [SerializeField] protected Vector3 rotationAxis = Vector3.forward;

    private Coroutine launchRoutine;
    private Tweener rotationTweener;
    protected GameObject owner;

    protected virtual void OnEnable()
    {
        Invoke(nameof(Despawn), lifetime);
    }

    public virtual void Launch(Vector3 direction, GameObject shooter)
    {
        owner = shooter;

        if (launchRoutine != null)
            StopCoroutine(launchRoutine);

        launchRoutine = StartCoroutine(LaunchRoutine(direction));
    }


    private IEnumerator LaunchRoutine(Vector3 direction)
    {
        yield return new WaitForSeconds(attackDelay);

        if (rb != null)
            rb.linearVelocity = direction.normalized * speed;

        if (enableRotation)
            StartRotationAnimation();
    }

    protected virtual void StartRotationAnimation()
    {
        if (rotationTweener != null)
            rotationTweener.Kill();

        rotationTweener = transform.DORotate(rotationAxis.normalized * 360f, 360f / rotationSpeed, RotateMode.LocalAxisAdd)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Incremental);
    }


    protected virtual void OnTriggerEnter(Collider other)
    {
        if (owner != null && other.gameObject == owner)
            return;
        Health target = other.GetComponent<Health>();
        if (target != null)
        {
            target.TakeDamage(damage);
        }
        Despawn();
    }

    protected virtual void Despawn()
    {
        CancelInvoke();

        if (rotationTweener != null)
            rotationTweener.Kill();

        if (rb != null)
            rb.linearVelocity = Vector3.zero;

        gameObject.SetActive(false);
    }
}