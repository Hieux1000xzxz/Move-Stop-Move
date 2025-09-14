using DG.Tweening;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

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
    protected Vector3 launchPos;
    protected Tween rotateTween;

    public bool isFlying;
    public bool IsFlying => isFlying;

    private bool isFollowing = false;

    public virtual void Init(CharacterBase character, Transform hand)
    {
        owner = character;
        spawnPoint = hand;

        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(handRotationOffset);

        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned && NetworkManager.Singleton.IsServer)
        {
            netObj.Spawn(true);
        }

        isFollowing = true;
        isFlying = false;

        rb.isKinematic = true;
    }

    private void LateUpdate()
    {
        if (isFollowing && !isFlying && spawnPoint != null)
        {
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation * Quaternion.Euler(handRotationOffset);
        }
    }

    public virtual void Launch(Vector3 dir, GameObject shooter)
    {
        if (isFlying)
        {
            Debug.LogWarning($"{name} đang bay rồi, không thể bắn lại!");
            return;
        }

        if (rb == null)
        {
            Debug.LogError($"{name} KHÔNG có Rigidbody!");
            return;
        }

        isFlying = true;
        isFollowing = false;

        rb.isKinematic = false;
        transform.position = spawnPoint.position;
        transform.rotation = spawnPoint.rotation * Quaternion.Euler(handRotationOffset);
        rb.linearVelocity = dir * speed;

        Debug.Log($"{name} được bắn từ vị trí {spawnPoint.position} theo hướng {dir}, tốc độ {speed}");
        launchPos = transform.position;

        StartRotation();
    }

    protected virtual void ReturnToHand()
    {
        if (spawnPoint == null) return;

        StopRotation();
        isFlying = false;
        isFollowing = true;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;

        transform.position = spawnPoint.position;
        transform.rotation = spawnPoint.rotation * Quaternion.Euler(handRotationOffset);

        owner?.OnWeaponReturned();
    }

    protected virtual void Update()
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

        CharacterBase victim = other.GetComponent<CharacterBase>();
        if (victim != null && victim != owner)
        {
            NotifyHitServerRpc(victim.NetworkObject);
            ReturnToHand();
        }
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

    [ServerRpc(RequireOwnership = false)]
    private void NotifyHitServerRpc(NetworkObjectReference victimRef)
    {
        if (!victimRef.TryGet(out NetworkObject victimObj)) return;

        CharacterBase victim = victimObj.GetComponent<CharacterBase>();
        if (victim == null || victim == owner) return;

        Health h = victim.GetComponent<Health>();
        if (h != null)
        {
            h.ApplyDamage(damage);
        }

        owner.AddScore(1);

        if (victim.characterCollider != null)
        {
            victim.characterCollider.enabled = false;
        }
    }
}