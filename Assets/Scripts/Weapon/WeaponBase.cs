using DG.Tweening;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class WeaponBase : NetworkBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] public float speed = 12f;
    [SerializeField] private int damage = 1;
    [SerializeField] private Vector3 handRotationOffset = Vector3.zero;

    [Header("Rotation Settings")]
    [SerializeField] private Vector3 rotateAxis = new Vector3(0, 1, 0);
    [SerializeField] private float rotateSpeed = 360f;
    [SerializeField] private RotateMode rotateMode = RotateMode.FastBeyond360;

    [Header("Cached References")]
    [SerializeField] private Rigidbody rb;           
    [SerializeField] private Collider col;            
    [SerializeField] private NetworkObject netObj;    

    private CharacterBase owner;
    private Transform spawnPoint;
    private Vector3 launchPos;
    private Tween rotateTween;

    private float originalSpeed;
    private Vector3 baseScale;
    private bool isFollowing;
    private bool isFlying;
    
    private static readonly Dictionary<NetworkObject, WeaponBase> weaponCache = new();

    public float OriginalSpeed => originalSpeed;
    public bool IsFlying => isFlying;
    public Vector3 BaseScale => baseScale;
    
    public NetworkObject NetworkObj => netObj;
    public float BuffScaleMultiplier { get; set; } = 1f;
    public bool IsBeingReleased { get; private set; } = false;

    public void MarkAsReleased()
    {
        IsBeingReleased = true;
    }

    #region INIT
    private void Awake()
    {
        ObjectPool.Instance?.RegisterNetworkObject(gameObject, GetComponent<NetworkObject>());
        
        baseScale = transform.localScale;
        originalSpeed = speed;
    }

    public void Init(CharacterBase character, Transform hand)
    {
        owner = character;
        spawnPoint = hand;

        transform.position = hand.position;
        transform.rotation = hand.rotation * Quaternion.Euler(handRotationOffset);

        TrySpawnNetworkObject();

        isFollowing = true;
        isFlying = false;
        rb.isKinematic = true;

        StartRotation();
    }

    private void TrySpawnNetworkObject()
    {
        if (netObj && !netObj.IsSpawned && NetworkManager.Singleton.IsServer)
        {
            netObj.Spawn(true);
        }
    }
    #endregion

    #region FOLLOW HAND
    private void LateUpdate()
    {
        if (isFollowing && !isFlying && spawnPoint != null)
        {
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation * Quaternion.Euler(handRotationOffset);
        }

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
        if (col != null)
        {
            col.enabled = false;
            StartCoroutine(EnableColliderNextFrame());
        }
        transform.position = spawnPoint.position;
        rb.linearVelocity = dir * speed;
        launchPos = transform.position;

        StartRotation();
    }
    private IEnumerator EnableColliderNextFrame()
    {
        yield return null; 
        if (col != null) col.enabled = true;
    }

    protected void ReturnToHand()
    {
        isFlying = false;
        isFollowing = true;

        StopRotation();

        if (rb) rb.isKinematic = true;
        if (col) col.enabled = true;

        if (spawnPoint == null && owner != null)
            spawnPoint = owner.weaponSpawnPoint;

        if (spawnPoint)
        {
            transform.position = spawnPoint.position;
            transform.rotation = spawnPoint.rotation * Quaternion.Euler(handRotationOffset);
        }

        owner?.OnWeaponReturned();
        StartRotation();
    }
    #endregion

    #region Collision
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
        if (!IsServer || !isFlying) return;
        if (other.gameObject == owner?.gameObject) return;

        if (col)
            StartCoroutine(ReenableColliderNextFrame());

        if (other.CompareTag("Wall"))
        {
            ReturnToHand();
            ReturnToHandClientRpc();

            var safe = other.GetComponent<NavMeshSafeObstacle>();
            if (safe != null)
                safe.DisableAndHide();   // ✅ gọi hàm an toàn
            else
                other.gameObject.SetActive(false); // fallback nếu chưa có script

            return;
        }


        CharacterBase victim = other.GetComponent<CharacterBase>();
        if (victim != null && victim != owner)
        {
            ApplyDamageAndScore(victim);
            ReturnToHand();
            ReturnToHandClientRpc();
        }
    }

    private IEnumerator ReenableColliderNextFrame()
    {
        col.enabled = false;
        yield return null;
        if (isFlying)
            col.enabled = true;
    }

    private void ApplyDamageAndScore(CharacterBase victim)
    {
        if (victim == null || victim == owner) return;

        victim.health?.ApplyDamage(damage);
        owner?.AddScore(1);

        if (victim.characterCollider != null)
            victim.characterCollider.enabled = false;
    }
    #endregion

    #region ROTATION
    protected void StartRotation()
    {
        StopRotation();
        if (transform == null) return;
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
    private void ReturnToHandServerRpc()
    {
        ReturnToHand();
        ReturnToHandClientRpc();
    }

    [ClientRpc]
    private void ReturnToHandClientRpc()
    {
        if (!NetworkManager.Singleton.IsServer)
            ReturnToHand();
    }
    [ClientRpc]
    private void HideWallClientRpc(NetworkObjectReference wallRef)
    {
        if (wallRef.TryGet(out NetworkObject wallObj))
        {
            wallObj.gameObject.SetActive(false);
        }
    }
    #endregion

    #region UTILITY
    public void SetOwner(CharacterBase newOwner)
    {
        owner = newOwner;
        spawnPoint = newOwner ? newOwner.weaponSpawnPoint : null;
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
        transform.localScale = baseScale * BuffScaleMultiplier * ownerScale;
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (netObj != null && !weaponCache.ContainsKey(netObj))
            weaponCache.Add(netObj, this);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (netObj != null)
            weaponCache.Remove(netObj);
    }

    public static WeaponBase GetByNetworkObject(NetworkObject net)
    {
        if (net == null) return null;
        weaponCache.TryGetValue(net, out var weapon);
        return weapon;
    }

    #endregion
}
