using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode.Components;

public enum CharacterState
{
    Idle,
    Move,
    Attack
}

public abstract partial class CharacterBase : NetworkBehaviour
{
    public enum OwnerType
    {
        Player,
        AI
    }

    [Header("Character Settings")] [SerializeField]
    private float moveSpeed = 5f;

    [SerializeField] private NavMeshAgent agent;
    [SerializeField] protected Animator animator;
    [SerializeField] private NetworkAnimator networkAnimator;
    [SerializeField] private Health health;
    [SerializeField] protected float attackRange = 2f;
    [SerializeField] protected float attackDuration = 0.5f;
    [SerializeField] private Collider characterCollider;
    [SerializeField] protected NetworkObject networkObject;

    [Header("Weapon Settings")] [SerializeField]
    private Transform weaponSpawnPoint;

    [SerializeField] protected Vector3 weaponRotationOffset = Vector3.zero;
    [SerializeField] protected float attackDelay = 0.5f;
    [SerializeField] private WeaponType weaponType;
    protected WeaponBase currentWeapon;

    [Header("Score Settings")] [SerializeField]
    protected KillScoreDisplay scoreDisplay;

    [SerializeField] private float sizePerScore = 0.05f;
    [SerializeField] private float rangePerScore = 0.1f;
    [SerializeField] private float moveSpeedPerScore = 0.1f;
    [SerializeField] private float maxScale = 3f;

    [Header("Default Weapon")] [SerializeField]
    private WeaponType defaultWeapon;

    [Header("Character Owner")] public OwnerType ownerType = OwnerType.Player;

    [Header("Attack Settings")] [SerializeField]
    private float detectAttackDelay = 0.5f;

    protected CharacterState currentState = CharacterState.Idle;
    protected Transform attackTarget;
    protected Transform detectedTarget;
    protected bool isAttacking = false;
    protected bool hasWeapon = true;
    protected float nextAttackTime = 0f;
    public bool isDead = false;
    private Vector3 lastPosition;
    private Coroutine attackRoutine;

    private Coroutine speedBoostRoutine;
    private Coroutine weaponGrowRoutine;
    private bool isSpeedBoostActive = false;
    private bool isWeaponGrowActive = false;
    private bool hasDied = false;

    private float baseMoveSpeed;

    public float currentAttackRange => attackRange;
    public WeaponBase currentWeaponPublic => currentWeapon;
    public float MoveSpeed => moveSpeed;
    public NavMeshAgent Agent => agent;
    public bool IsHealthDead => health != null && health.IsDead;
    public Health HealthComponent => health;
    public Transform WeaponSpawnPoint => weaponSpawnPoint;
    public WeaponType WeaponType => weaponType;

    public Collider CharacterCollider => characterCollider;

    public NetworkAnimator NetworkAnimator => networkAnimator;

    protected bool IsMultiplayer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    protected bool ShouldProcessInput => !IsMultiplayer || IsOwner;

    public NetworkVariable<int> Score = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);


    public NetworkVariable<FixedString32Bytes> PlayerName = new NetworkVariable<FixedString32Bytes>(
        "Player",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<NetworkObjectReference> NetCurrentWeapon =
        new NetworkVariable<NetworkObjectReference>(default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public NetworkVariable<CharacterState> NetState = new NetworkVariable<CharacterState>(
        CharacterState.Idle,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> SessionCoin = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    protected NetworkVariable<float> netSpeed = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    protected NetworkVariable<bool> netIsAttacking = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private readonly Collider[] overlapResults = new Collider[32];
    private int overlapCount;

    #region Unity Lifecycle

    protected bool AgentValid => agent != null && agent.isActiveAndEnabled;

    protected float DistanceTo(Transform t) => Vector3.Distance(transform.position, t.position);

    protected virtual void Awake()
    {
        ObjectPool.Instance.RegisterNetworkObject(gameObject, GetComponent<NetworkObject>());
        ObjectPool.Instance.RegisterCharacter(gameObject, this);
    }

    protected virtual void Start()
    {
        baseMoveSpeed = moveSpeed;

        if (agent != null)
        {
            agent.speed = moveSpeed;
            lastPosition = transform.position;
        }

        OnWeaponReturned();
    }

    protected virtual void Update()
    {
        if (ShouldSkipUpdate())
            return;
        ProcessInput();
        ProcessRadar();
        ProcessCurrentState();
        UpdateAnimator();
    }

    private bool ShouldSkipUpdate()
    {
        CheckForDead();
        return !IsValidForUpdate();
    }

    private bool IsValidForUpdate()
    {
        return GameManager.Instance &&
               GameManager.Instance.IsGameStarted &&
               !isDead &&
               !health.IsDead &&
               agent != null &&
               agent.isActiveAndEnabled;
    }

    private void ProcessInput()
    {
        Vector3 input = GetMovementInput();

        if (ShouldCancelAttack(input))
            EndAttack(true);
    }

    private bool ShouldCancelAttack(Vector3 input)
    {
        return currentState == CharacterState.Attack &&
               isAttacking &&
               input.magnitude > 0.01f;
    }

    private void ProcessRadar()
    {
        if (!IsMovingNow())
            UpdateRadar();
        else
            detectedTarget = null;
    }

    private void ProcessCurrentState()
    {
        switch (currentState)
        {
            case CharacterState.Idle:
                HandleIdle();
                break;
            case CharacterState.Move:
                HandleMove();
                break;
            case CharacterState.Attack:
                HandleAttack();
                break;
        }
    }

    protected virtual void OnDisable()
    {
        attackTarget = null;
        detectedTarget = null;
    }

    #endregion
}