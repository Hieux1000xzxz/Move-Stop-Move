using DG.Tweening;
using Unity.Netcode;
using UnityEngine;

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
    //[SerializeField] private NetworkObject netObj;
    protected CharacterBase owner;
    protected Transform spawnPoint;
    protected Vector3 originalPos;
    protected Quaternion originalRot;
    protected bool isFlying;
    protected Vector3 launchPos;
    protected Tween rotateTween;

    public bool IsFlying => isFlying;

    //public NetworkObject NetObj => netObj;  
    //protected virtual void Awake()
    //{
    //    if (netObj == null)
    //        netObj = GetComponent<NetworkObject>();
    //}
    public virtual void Init(CharacterBase character, Transform hand)
    {
        owner = character;
        spawnPoint = hand;

        //transform.SetParent(spawnPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(handRotationOffset);

        originalRot = transform.localRotation;
    }
    public virtual void Launch(Vector3 dir, GameObject shooter)
    {
        if (isFlying) return;

        //if (NetworkManager.Singleton.IsServer) NetObj.TrySetParent((Transform)null, false);
        //transform.SetParent(null, true);

        rb.isKinematic = false;
        transform.position = spawnPoint.position;
        transform.rotation = spawnPoint.rotation * Quaternion.Euler(handRotationOffset);
        rb.linearVelocity = dir * speed;

        launchPos = transform.position;
        isFlying = true;
        StartRotation();
    }

    protected virtual void ReturnToHand()
    {
        if (spawnPoint == null) return;

        rb.isKinematic = true;
        StopRotation();

        //transform.SetParent(spawnPoint, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(handRotationOffset);

        //if (NetworkManager.Singleton.IsServer) NetObj.TrySetParent(spawnPoint, false);

        isFlying = false;
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
