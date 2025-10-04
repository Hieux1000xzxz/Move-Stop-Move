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
    
    #region INIT
    public void Init(CharacterBase character, Transform hand)
    {
        owner = character;
        spawnPoint = hand;
        baseScale = transform.localScale;
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.Euler(handRotationOffset);

        originalSpeed = speed;

        TrySpawnNetworkObject();
        
        isFollowing = true;
        isFlying = false;

        rb.isKinematic = true;

        StartRotation();

    }

    private void TrySpawnNetworkObject()
    {
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned && NetworkManager.Singleton.IsServer)
        {
            netObj.Spawn(true);
        }
    }
    #endregion
    
    #region FOLLOW HAND
    private void LateUpdate()
    {
        FollowHandIfNeeded();
        UpdateScale();
    }
    private void FollowHandIfNeeded()
    {
        if (isFollowing && !isFlying && spawnPoint != null)
        {
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation * Quaternion.Euler(handRotationOffset);
        }
    }
    private void UpdateScale()
    {
        if (baseScale == Vector3.zero)
            baseScale = Vector3.one;

        transform.localScale = baseScale * BuffScaleMultiplier;
    }
    #endregion
    
    #region Launch & Return
    public void Launch(Vector3 dir, GameObject shooter)
    {
        if (isFlying || rb == null) return;

        isFlying = true;
        isFollowing = false;
        rb.isKinematic = false;
        
        transform.position = spawnPoint.position;
        rb.linearVelocity = dir * speed;
        launchPos = transform.position;

        StartRotation();
    }

    protected void ReturnToHand()
    {
        ResetState();
        ResetPhysics();
        EnsureSpawnPoint();
        ValidateOwner();
        SnapToHand();
        NotifyOwnerReturned();
        RestartRotation();
    }
    private void ResetState()
    {
        hasHit = false;
        isFlying = false;
        isFollowing = true;
        StopRotation();
    }
    private void ResetPhysics()
    {
        if (rb != null) rb.isKinematic = true;
        if (rb != null && collider != null) collider.enabled = true;
    }
    private void EnsureSpawnPoint()
    {
        if (spawnPoint == null && owner != null)
            spawnPoint = owner.weaponSpawnPoint;
    }
    private bool ValidateOwner()
    {
        if (owner == null)
        {
            return false;
        }
        return true;
    }
    private void SnapToHand()
    {
        transform.position = spawnPoint.position;
        transform.rotation = spawnPoint.rotation * Quaternion.Euler(handRotationOffset);
    }
    private void NotifyOwnerReturned()
    {
        owner?.OnWeaponReturned();
    }
    private void RestartRotation()
    {
        StartRotation();
    }
    #endregion
    
    #region Collision
    protected virtual void Update()
    {
        CheckMaxDistance();
    }
    private void CheckMaxDistance()
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
        if (!IsServer || !isFlying) return;
        if (other.gameObject == owner?.gameObject) return;

        HandleTemporaryColliderDisable();

        if (IsWall(other))
        {
            HandleWallCollision();
            return;
        }

        CharacterBase victim = other.GetComponent<CharacterBase>();
        if (victim != null && victim != owner)
        {
            HandleVictimHit(victim);
        }
    }
    private void HandleTemporaryColliderDisable()
    {
        if (collider != null)
            StartCoroutine(ReenableColliderNextFrame());
    }
    private bool IsWall(Collider other)
    {
        return other.CompareTag("Wall");
    }
    private void HandleWallCollision()
    {
        ReturnToHand();
        ReturnToHandClientRpc();
    }
    private void HandleVictimHit(CharacterBase victim)
    {
        hasHit = true;

        if (IsServer)
        {
            ApplyDamageAndScore(victim);
            ReturnToHand();
            ReturnToHandClientRpc();
        }
        else
        {
            NotifyHitServerRpc(victim.NetworkObject);
        }
    }
    private void ApplyDamageAndScore(CharacterBase victim)
    {
        if (victim == null || victim == owner) return;

        Health h = victim.GetComponent<Health>();
        if (h != null)
        {
            h.ApplyDamage(damage);
        }

        owner?.AddScore(1);

        if (victim.characterCollider != null)
            victim.characterCollider.enabled = false;
    }
    private IEnumerator ReenableColliderNextFrame()
    {
        collider.enabled = false;
        yield return null; 
        if (isFlying) 
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

    #endregion
    
    #region ROTATION
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
    #endregion
    
    #region NETWORK
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
    #endregion
    
    #region UTILITY
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
    
    public void ApplyScale(float ownerScale = 1f)
    {
        if (baseScale == Vector3.zero)
            baseScale = Vector3.one;

        transform.localScale = baseScale * BuffScaleMultiplier * ownerScale;
    }
    #endregion
}