using DG.Tweening;
using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class WeaponBase : NetworkBehaviour 
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

        StartRotation();

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
        transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(handRotationOffset);
        rb.linearVelocity = dir * speed;
        rb.angularVelocity = dir.normalized * rotateSpeed * Mathf.Deg2Rad;
        launchPos = transform.position;

        StartRotation();
    }

    protected virtual void ReturnToHand()
    {
        StopRotation();
        isFlying = false;
        isFollowing = true;
        rb.isKinematic = true;

        if (spawnPoint == null && owner != null)
        {
            spawnPoint = owner.weaponSpawnPoint;
        }

        if (owner == null)   // ❌ fix crash
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
        //if (!NetworkManager.Singleton.IsServer) return; 
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
        if (!isFlying || other.gameObject == owner.gameObject) return;

        if(other.CompareTag("Wall"))
        {
            if(NetworkManager.Singleton.IsServer)
            {
                ReturnToHandServerRpc();
            }

            NetworkObject wallNetObj = other.GetComponent<NetworkObject>();
            if (wallNetObj != null && wallNetObj.IsSpawned)
            {
                wallNetObj.Despawn(true); 
            }
            else
            {
                Destroy(other.gameObject); 
            }
        }
        CharacterBase victim = other.GetComponent<CharacterBase>();
        if (victim != null && victim != owner)
        {
            NotifyHitServerRpc(victim.NetworkObject);

            if (NetworkManager.Singleton.IsServer)
            {
                ReturnToHandServerRpc();
            }
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
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
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
        owner = null;
        spawnPoint = null;
        
        StopRotation();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        StopRotation();
    }
}