using DG.Tweening;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class WeaponBase : NetworkBehaviour
{
    [Header("Weapon Settings")] [SerializeField]
    private float speed = 12f;

    [SerializeField] private int damage = 1;
    [SerializeField] private Vector3 handRotationOffset = Vector3.zero;

    [Header("Rotation Settings")] [SerializeField]
    private Vector3 rotateAxis = new Vector3(0, 1, 0);

    [SerializeField] private float rotateSpeed = 360f;
    [SerializeField] private RotateMode rotateMode = RotateMode.FastBeyond360;

    [Header("Cached References")] [SerializeField]
    private Rigidbody rb;

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

    public NetworkObject NetworkObj => netObj;
    public float BuffScaleMultiplier { get; set; } = 1f;

    public float Speed
    {
        get => speed;
        set => speed = value;
    }

    #region INIT

    private void Awake()
    {
        ObjectPool.Instance.RegisterNetworkObject(gameObject, GetComponent<NetworkObject>());

        baseScale = transform.localScale;
        originalSpeed = speed;
    }

    public void Init(CharacterBase character, Transform hand)
    {
        owner = character;
        spawnPoint = hand;

        transform.SetPositionAndRotation(
            hand.position,
            hand.rotation * Quaternion.Euler(handRotationOffset)
        );

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
            transform.SetPositionAndRotation(
                spawnPoint.position,
                spawnPoint.rotation * Quaternion.Euler(handRotationOffset)
            );
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
        if (col != null && owner != null && owner.CharacterCollider != null)
        {
            Physics.IgnoreCollision(col, owner.CharacterCollider, true);
            StartCoroutine(ReEnableCollisionWithOwner());
        }

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

    private IEnumerator ReEnableCollisionWithOwner()
    {
        yield return new WaitForSeconds(0.1f);

        if (col != null && owner != null && owner.CharacterCollider != null)
        {
            Physics.IgnoreCollision(col, owner.CharacterCollider, false);
        }
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
            spawnPoint = owner.WeaponSpawnPoint;

        if (spawnPoint)
            transform.SetPositionAndRotation(
                spawnPoint.position,
                spawnPoint.rotation * Quaternion.Euler(handRotationOffset)
            );

        owner.OnWeaponReturned();
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
        if (other.gameObject == owner.gameObject) return;

        if (col)
            StartCoroutine(ReenableColliderNextFrame());

        if (HandleWallCollision(other)) return;

        HandleCharacterCollision(other);
    }

    private bool HandleWallCollision(Collider other)
    {
        if (!other.CompareTag("Wall"))
            return false;

        ReturnToHand();
        ReturnToHandClientRpc();

        if (other.TryGetComponent<NavMeshSafeObstacle>(out var safe))
            safe.DisableAndHide();
        else
            other.gameObject.SetActive(false);

        return true;
    }

    private void HandleCharacterCollision(Collider other)
    {
        if (!other.TryGetComponent<CharacterBase>(out var victim))
            return;

        if (victim == owner)
            return;

        ApplyDamageAndScore(victim);
        ReturnToHand();
        ReturnToHandClientRpc();
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

        victim.HealthComponent.ApplyDamage(damage);
        owner.AddScore(1);

        if (victim.CharacterCollider != null)
            victim.CharacterCollider.enabled = false;
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

    #endregion

    #region UTILITY

    public void SetOwner(CharacterBase newOwner)
    {
        owner = newOwner;
        spawnPoint = newOwner ? newOwner.WeaponSpawnPoint : null;
    }

    public void ClearOwner()
    {
        StopRotation();
        gameObject.SetActive(false);
    }

    protected new void OnDestroy()
    {
        StopRotation();
    }

    public void ApplyScale(float ownerScale = 1f)
    {
        transform.localScale = baseScale * (BuffScaleMultiplier * ownerScale);
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
        weaponCache.TryGetValue(net, out WeaponBase weapon);
        return weapon;
    }

    #endregion
}