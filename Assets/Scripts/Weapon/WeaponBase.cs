using UnityEngine;
using DG.Tweening;

public class WeaponBase : MonoBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] protected float speed = 12f;
    [SerializeField] protected int damage = 1;
    [SerializeField] protected Vector3 handRotationOffset = Vector3.zero;

    [Header("Rotation Settings")]
    [SerializeField] protected Vector3 rotateAxis = new Vector3(0, 1, 0);
    [SerializeField] protected float rotateSpeed = 360f;
    [SerializeField] protected RotateMode rotateMode = RotateMode.FastBeyond360;

    [SerializeField] protected Rigidbody rb;

    protected CharacterBase owner;
    protected Transform spawnPoint;
    protected Vector3 originalPos;
    protected Quaternion originalRot;
    protected bool isFlying;
    protected Vector3 launchPos;
    protected Tween rotateTween;

    public bool IsFlying => isFlying;

    public virtual void Init(CharacterBase character, Transform hand)
    {
        owner = character;
        spawnPoint = hand;

        originalPos = transform.localPosition;
        originalRot = transform.localRotation * Quaternion.Euler(handRotationOffset);

        ReturnToHand();
    }

    public virtual void Launch(Vector3 dir, GameObject shooter)
    {
        if (isFlying) return;

        transform.SetParent(null);
        rb.isKinematic = false;
        rb.linearVelocity = dir * speed;

        launchPos = transform.position;
        isFlying = true;

        StartRotation();
    }

    protected virtual void FixedUpdate()
    {
        if (isFlying && owner != null)
        {
            float dist = Vector3.Distance(launchPos, transform.position);
            if (dist >= owner.currentAttackRange)
            {
                ReturnToHand();
            }
        }
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (!isFlying || other.gameObject == owner.gameObject) return;

        if (other.CompareTag("Enemy"))
        {
            Health h = other.GetComponent<Health>();
            if (h != null) h.TakeDamage(damage);
            owner.AddScore(1);
            ReturnToHand();
        }
    }

    protected virtual void ReturnToHand()
    {
        rb.isKinematic = true;

        StopRotation();

        transform.SetParent(spawnPoint);
        transform.localPosition = originalPos;
        transform.localRotation = originalRot;

        isFlying = false;

        if (owner != null)
            owner.OnWeaponReturned();
    }

    public virtual void ResetWeapon()
    {
        ReturnToHand();
    }

    protected void StartRotation()
    {
        StopRotation();
        rotateTween = transform.DOLocalRotate(rotateAxis * rotateSpeed, 1f, rotateMode)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Incremental);
    }

    protected void StopRotation()
    {
        if (rotateTween != null && rotateTween.IsActive())
        {
            rotateTween.Kill();
            rotateTween = null;
        }
    }

    public virtual void ApplyData(WeaponData data)
    {
        if (data == null) return;
        speed = data.speed;
        damage = data.damage;
    }

}
