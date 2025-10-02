using DG.Tweening;
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using System.Collections;
public class WeaponBase : NetworkBehaviour 
{
    [Header("Weapon Settings")]
    [SerializeField] public float speed = 12f;
    [SerializeField] protected int damage = 1;
    [SerializeField] protected Vector3 handRotationOffset = Vector3.zero;

    [Header("Rotation Settings")]
    [SerializeField] protected Vector3 rotateAxis = new Vector3(0, 1, 0);
    [SerializeField] protected float rotateSpeed = 360f;
    [SerializeField] protected RotateMode rotateMode = RotateMode.FastBeyond360;

    [SerializeField] protected Rigidbody rb;
    [SerializeField] protected Collider collider;
    protected CharacterBase owner;
    protected Transform spawnPoint;
    protected Vector3 launchPos;
    protected Tween rotateTween;
    
    private float originalSpeed;
    public float OriginalSpeed => originalSpeed;
    
    public bool isFlying;
    public bool IsFlying => isFlying;
    private Vector3 baseScale;
    public Vector3 BaseScale => baseScale;
    private bool isFollowing = false;
    
    private bool hasHit = false;
    
    public float BuffScaleMultiplier { get; set; } = 1f;
    public virtual void Init(CharacterBase character, Transform hand)
    {
        owner = character;
        spawnPoint = hand;
        baseScale = transform.localScale;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(handRotationOffset);

        originalSpeed = speed;
        
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned && NetworkManager.Singleton.IsServer)
        {
            netObj.Spawn(true);
        }

        isFollowing = true;
        isFlying = false;

        rb.isKinematic = true;

        StartRotation();

    }

    private void LateUpdate()
    {
        if (isFollowing && !isFlying && spawnPoint != null)
        {
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation * Quaternion.Euler(handRotationOffset);
        }
        
        transform.localScale = baseScale * BuffScaleMultiplier;
    }

    public virtual void Launch(Vector3 dir, GameObject shooter)
    {
        bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        bool isClient = NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient;

        if (isFlying)
        {
            return;
        }

        if (rb == null)
        {
            return;
        }

        isFlying = true;
        isFollowing = false;

        rb.isKinematic = false;
        transform.position = spawnPoint.position;
        rb.linearVelocity = dir * speed;
        launchPos = transform.position;

        StartRotation();
    }

    protected virtual void ReturnToHand()
    {
        hasHit  = false; 
        StopRotation();
        isFlying = false;
        isFollowing = true;
        rb.isKinematic = true;
        
        if (rb != null && collider != null)
        {
            collider.enabled = true; 
        }
        
        if (spawnPoint == null && owner != null)
        {
            spawnPoint = owner.weaponSpawnPoint;
        }

        if (owner == null)   // fix crash
        {
            Debug.LogWarning($"[WeaponBase] ReturnToHand called but spawnPoint is null for {gameObject.name}");
            return;
        }

        transform.position = spawnPoint.position;
        transform.rotation = spawnPoint.rotation * Quaternion.Euler(handRotationOffset);

        owner?.OnWeaponReturned();

        StartRotation();
    }

    protected virtual void Update()
    {
        if (isFlying && owner != null)
        {
            float dist = Vector3.Distance(launchPos, transform.position);
            if (dist >= owner.currentAttackRange)
            {
                ReturnToHandServerRpc();
            }
        }
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;
        if (!isFlying || other.gameObject == owner.gameObject) return;

        if (collider != null)
        {
            StartCoroutine(ReenableColliderNextFrame());
        }

        if (other.CompareTag("Wall"))
        {
            if (IsServer)
            {
                ReturnToHand(); 
                ReturnToHandClientRpc();
            }
            return;
        }

        CharacterBase victim = other.GetComponent<CharacterBase>();
        if (victim != null && victim != owner)
        {
            hasHit = true;

            if (IsServer)
            {
                HandleHit(victim);
                ReturnToHand(); 
                ReturnToHandClientRpc();
            }
            else
            {
                NotifyHitServerRpc(victim.NetworkObject);
            }
        }
    }
    private IEnumerator ReenableColliderNextFrame()
    {
        collider.enabled = false;
        yield return null; // chờ 1 frame
        if (isFlying) // chỉ bật lại nếu weapon vẫn đang bay
            collider.enabled = true;
    }
    private void HandleHit(CharacterBase victim)
    {
        if (victim == null || victim == owner) return;

        Health h = victim.GetComponent<Health>();
        if (h != null)
        {
            h.ApplyDamage(damage);
        }

        owner?.AddScore(1);

        if (victim.characterCollider != null)
        {
            victim.characterCollider.enabled = false;
        }
    }

    public virtual void ResetWeapon()
    {
        ReturnToHand();
    }

    protected void StartRotation()
    {
        StopRotation();
        if (this == null || transform == null) return;
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

    [ServerRpc(RequireOwnership = false)]
    private void NotifyHitServerRpc(NetworkObjectReference victimRef)
    {
        if (!victimRef.TryGet(out NetworkObject victimObj)) return;
        HandleHit(victimObj.GetComponent<CharacterBase>());
        ReturnToHand();
        ReturnToHandClientRpc();
    }

    
    [ServerRpc(RequireOwnership = false)]
    private void ReturnToHandServerRpc()
    {
        ReturnToHand();
        ReturnToHandClientRpc();
    }

    [ClientRpc]
    private void ReturnToHandClientRpc()
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            ReturnToHand();
        }
    }
    
    [ClientRpc]
    private void DisableColliderClientRpc()
    {
        if (collider != null)
        {
            collider.enabled = false;
        }
    }


    public void SetOwner(CharacterBase newOwner)
    {
        owner = newOwner;
        if (newOwner != null)
        {
            spawnPoint = newOwner.weaponSpawnPoint;
        }
    }

    public void ClearOwner()
    {        
        StopRotation();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        StopRotation();
    }
}