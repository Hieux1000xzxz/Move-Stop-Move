using DG.Tweening;
using System.Collections;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Splines;
using static UnityEngine.Rendering.DebugUI.Table;

public enum CharacterState { Idle, Move, Attack }

public abstract class CharacterBase : NetworkBehaviour
{
    public enum OwnerType { Player, AI }

    [Header("Character Settings")]
    [SerializeField] public float moveSpeed = 5f;
    [SerializeField] public NavMeshAgent agent;
    [SerializeField] protected Animator animator;
    [SerializeField] public Health health;
    [SerializeField] protected float attackRange = 2f;
    [SerializeField] protected float attackDuration = 0.5f;
    [SerializeField] protected LayerMask targetLayer;
    [SerializeField] public Collider characterCollider;
    [SerializeField] protected NetworkObject networkObject;

    [Header("Weapon Settings")]
    [SerializeField] public Transform weaponSpawnPoint;
    [SerializeField] protected Vector3 weaponRotationOffset = Vector3.zero;
    [SerializeField] protected float attackDelay = 0.5f;
    [SerializeField] public WeaponType weaponType;
    protected WeaponBase currentWeapon;

    [Header("Score Settings")]
    [SerializeField] protected KillScoreDisplay scoreDisplay;
    [SerializeField] private float sizePerScore = 0.05f;
    [SerializeField] private float rangePerScore = 0.1f;
    [SerializeField] private float moveSpeedPerScore = 0.1f;
    [SerializeField] private float maxScale = 3f;

    [Header("Default Weapon")]
    [SerializeField] private WeaponType defaultWeapon;

    [Header("Character Owner")]
    [SerializeField] public OwnerType ownerType = OwnerType.Player;

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
    //private bool hasSpawnedBefore = false;

    // Powerup state
    private Coroutine speedBoostRoutine;
    private Coroutine weaponGrowRoutine;
    private bool isSpeedBoostActive = false;
    private bool isWeaponGrowActive = false;
    private bool hasDied = false;
    
    private float baseMoveSpeed;

    public float currentAttackRange => attackRange;
    public WeaponBase currentWeaponPublic => currentWeapon;

    public NetworkVariable<int> Score = new NetworkVariable<int>(
    0, NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> NetIsMoving = new NetworkVariable<bool>(
    false,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Owner);

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

    public NetworkVariable<bool> NetIsAttacking = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);


    #region Unity Lifecycle
    protected virtual void Awake()
    {
        ObjectPool.Instance?.RegisterNetworkObject(gameObject, GetComponent<NetworkObject>());
        ObjectPool.Instance?.RegisterCharacter(gameObject, this);
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
        if (!GameManager.Instance || !GameManager.Instance.IsGameStarted)
            return;

        CheckForDead();

        if (isDead || health.IsDead)
        {
            return;
        }
        
        Vector3 input = GetMovementInput();
        
        if (currentState == CharacterState.Attack && isAttacking && input.magnitude > 0.01f)
        {
            EndAttack(true);
        }
        
        if (agent == null || !agent.isActiveAndEnabled)
        {
            return;
        }

        if (!IsMovingNow()) 
        {
            UpdateRadar();
        }
        else
        {
            detectedTarget = null;  
        }

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

        UpdateAnimator();
    }

    protected virtual void OnDisable()
    {
        attackTarget = null;
        detectedTarget = null;
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
    
    #endregion

    #region MovementIsMov
    protected bool IsMovingNow()
    {
        if (this is Player player)
        {
            if (player.IsOwner)
                return player.isMovingInput;
            if (player.NetIsMoving.Value)
                return true;

            if (player.NetSpeed.Value > 0.1f)
                return true;

            if (agent != null && agent.isActiveAndEnabled)
                return agent.velocity.magnitude > 0.05f;

            return false;
        }

        // AI or non-player
        if (agent != null && agent.isActiveAndEnabled)
            return agent.velocity.magnitude > 0.05f;

        return false;
    }

    protected virtual void Move(Vector3 direction)
    {
        if (agent == null || !agent.isActiveAndEnabled) return;
        if (isDead) return;

        if (direction.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            agent.Move(direction.normalized * moveSpeed * Time.deltaTime);
        }
        else
        {
            agent.ResetPath();
        }
    }
    protected virtual void UpdateAnimator()
    {
        if (animator == null) return;

        float speed = 0f;
        if (agent != null && agent.isActiveAndEnabled)
        {
            speed = agent.velocity.magnitude;
        }
        else
        {
            Vector3 delta = (transform.position - lastPosition);
            delta.y = 0f;
            speed = (Time.deltaTime > 0f) ? (delta.magnitude / Time.deltaTime) : 0f;
        }

        animator.SetFloat("Speed", speed);

        bool attackingNow = IsOwner ? isAttacking : NetIsAttacking.Value;
        animator.SetBool("IsAttacking", attackingNow);

        lastPosition = transform.position;
    }

    
    public abstract Vector3 GetMovementInput();
    #endregion

    #region Radar & Tartget
    protected virtual void UpdateRadar()
    {
        Transform previousTarget = detectedTarget;
        detectedTarget = FindNearestTarget();

        if (detectedTarget != previousTarget)
        {
            OnTargetChanged(previousTarget, detectedTarget);
        }
    }
   
    protected virtual void OnTargetChanged(Transform oldTarget, Transform newTarget)
    {
        if (oldTarget != null && newTarget == null)
        {
            OnTargetLost(oldTarget);
        }
        else if (oldTarget == null && newTarget != null)
        {
            OnNewTargetFound(newTarget);
        }
        else if (oldTarget != null && newTarget != null && oldTarget != newTarget)
        {
            OnTargetSwitched(oldTarget, newTarget);
        }
    }

    protected virtual void OnTargetLost(Transform lostTarget)
    {
        if (attackTarget == lostTarget)
        {
            attackTarget = null;
            if (currentState == CharacterState.Attack)
            {
                StopAttackAnimationClientRpc();
                EndAttack(true);
                ChangeState(CharacterState.Idle);
            }
        }
    }

    protected virtual void OnNewTargetFound(Transform newTarget)
    {
        if (newTarget == null) return;
        if (isDead) return;
        
        StartCoroutine(DelayedAttackCheck(newTarget, detectAttackDelay));
    }
    private IEnumerator DelayedAttackCheck(Transform target, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (target != null && !isDead && currentState != CharacterState.Attack)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance <= attackRange && detectedTarget == target)
            {
                attackTarget = target;
                ChangeState(CharacterState.Attack);
            }
        }
    }
    protected virtual void OnTargetSwitched(Transform oldTarget, Transform newTarget)
    {
        if (attackTarget == oldTarget)
        {
            float distanceToNew = Vector3.Distance(transform.position, newTarget.position);
            if (distanceToNew <= attackRange)
            {
                attackTarget = newTarget;
            }
            else
            {
                EndAttack();
                ChangeState(CharacterState.Idle);
            }
        }
    }

    protected Transform FindNearestTarget()
    {
        Collider[] targets = GetTargetsInRange();

        Transform nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (Collider target in targets)
        {
            if (!IsValidTarget(target)) 
                continue;

            CharacterBase otherChar = GetCharacterBase(target);
            if (otherChar == null || otherChar == this) 
                continue;

            float distance = GetDistanceToTarget(otherChar);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = otherChar.transform;
            }
        }

        return nearest;
    }

    private Collider[] GetTargetsInRange()
    {
        return Physics.OverlapSphere(transform.position, attackRange);
    }

    private bool IsValidTarget(Collider target)
    {
        if (target == null) return false;
        if (target.transform == transform) return false;
        if (!target.gameObject.activeInHierarchy) return false;
        if (!target.CompareTag("Player")) return false;
        return true;
    }

    private CharacterBase GetCharacterBase(Collider target)
    {
        return target.GetComponent<CharacterBase>();
    }

    private float GetDistanceToTarget(CharacterBase otherChar)
    {
        return Vector3.Distance(transform.position, otherChar.transform.position);
    }

    #endregion
    
    #region State Handling
    protected void ChangeState(CharacterState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
        if (IsServer)
        {
            NetState.Value = newState; 
        }

        if (IsServer && newState == CharacterState.Attack)
        {
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
        }
    }

    protected  void HandleIdle()
    {
        Vector3 input = GetMovementInput();
        if (input.magnitude > 0.01f)
        {
            ChangeState(CharacterState.Move);
        }
        else
            CheckForAttack();
    }

    protected void HandleMove()
    {
        Vector3 input = GetMovementInput();
        if (input.magnitude > 0.01f)
        {
            Move(input);
        }
        else
        {
            ChangeState(CharacterState.Idle);
        }
    }

    protected void HandleAttack()
    {
        if (attackTarget == null || Vector3.Distance(transform.position, attackTarget.position) > attackRange)
        {
            if (!isAttacking)
            {
                EndAttack();
                ChangeState(CharacterState.Idle);
            }
            return;
        }
        
        FaceTarget(attackTarget.position);
        
        if (!isAttacking)
            PerformAttack();

        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }
    #endregion
    
    #region Attack
    protected virtual void CheckForAttack()
    {
        if (ShouldSkipAttackCheck()) return;

        HandleCurrentAttackTarget();
        HandleNewDetectedTarget();
    }

    private bool ShouldSkipAttackCheck()
    {
        if (IsMovingNow()) return true;
        if (currentState == CharacterState.Move) return true;

        if (this is Player player)
        {
            if (player.isMovingInput && player.NetIsMoving.Value)
                return true;
        }

        return false;
    }

    private void HandleCurrentAttackTarget()
    {
        if (attackTarget == null || attackTarget == detectedTarget) 
            return;

        float distance = Vector3.Distance(transform.position, attackTarget.position);
        if (distance > attackRange || !attackTarget.gameObject.activeInHierarchy)
        {
            attackTarget = null;
            EndAttack(true);
            ChangeState(CharacterState.Idle);
        }
    }

    private void HandleNewDetectedTarget()
    {
        if (detectedTarget == null) 
            return;

        float distance = Vector3.Distance(transform.position, detectedTarget.position);
        if (distance <= attackRange)
        {
            if (currentState != CharacterState.Attack || attackTarget != detectedTarget)
            {
                attackTarget = detectedTarget;
                ChangeState(CharacterState.Attack);
            }
        }
    }

    protected void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 30f);
        }
    }

    protected void PerformAttack()
    {
        if (!CanStartAttack())
            return;

        BeginAttack();
    }

    private bool CanStartAttack()
    {
        if (currentState != CharacterState.Idle && currentState != CharacterState.Attack)
            return false;

        if (agent != null && agent.isActiveAndEnabled && agent.velocity.magnitude > 0.01f)
            return false;

        if (currentWeapon == null)
        {
            StartCoroutine(WaitWeaponAndAttack());
            return false;
        }

        if (!hasWeapon) return false;
        if (currentWeapon.IsFlying) return false;
        if (Time.time < nextAttackTime) return false;

        return true;
    }

    private void BeginAttack()
    {
        isAttacking = true;
        if (IsServer) 
            NetIsAttacking.Value = true;

        hasWeapon = false;
        nextAttackTime = Time.time + attackDelay;

        animator?.SetBool("IsAttacking", true);

        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        attackRoutine = StartCoroutine(AttackRoutine());
    }

    private IEnumerator WaitWeaponAndAttack()
    {
        yield return new WaitUntil(() => currentWeapon != null);
        BeginAttack(); 
    }
  
    private IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(attackDelay);
        
        
        if (currentState != CharacterState.Attack || isDead || IsMovingNow())
        {
            EndAttack(true);
            yield break;
        }
        
        if (currentWeapon == null || currentWeapon.IsFlying)
        {
            EndAttack(true);
            yield break;
        }
        
        if (IsOwner)
        {
            RequestLaunchServerRpc();
        }

        yield return new WaitForSeconds(attackDuration);

        EndAttack();
    }

    public void OnWeaponReturned()
    {
        isAttacking = false;
        hasWeapon = true;
    }
    
    protected virtual void EndAttack(bool cancelByMove = false)
    {
        isAttacking = false;
        hasWeapon = true;

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        
        if (IsServer)
        {
            NetIsAttacking.Value = false;
            nextAttackTime = Time.time;
            
            if (IsMovingNow())
                ChangeState(CharacterState.Move);
            else
                ChangeState(CharacterState.Idle);

            animator?.SetBool("IsAttacking", false);
        }
    }
    
    #endregion

    #region  Score & Stats
    private void OnScoreChanged(int oldValue, int newValue)
    {
        if (scoreDisplay != null)
        {
            scoreDisplay.SetScore(newValue);
            UpdateCharacterStats();
        }
    }
    
    //sync Score
    public void AddScore(int value)
    {
        if (!IsServer) return;
        Score.Value += value;
    }
    private void UpdateCharacterStats()
    {
        if (scoreDisplay == null) return;

        UpdateScale();
        UpdateAttackRange();
        UpdateMoveSpeed();
    }
    private void UpdateScale()
    {
        float newScale = Mathf.Min(1f + scoreDisplay.CurrentScore * sizePerScore, maxScale);
        transform.localScale = Vector3.one * newScale;

        if (currentWeapon != null)
        {
            currentWeapon.ApplyScale(newScale);
        }
    }
    private void UpdateAttackRange()
    {
        attackRange += scoreDisplay.CurrentScore * rangePerScore;
    }
    private void UpdateMoveSpeed()
    {
        moveSpeed += moveSpeedPerScore;

        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.speed = moveSpeed;
        }
    }
    
    public void ResetState()
    {
        ResetCoreState();
        ResetHealth();
        ResetUIAndCollider();
        ResetWeapon();
        ResetAnimator();
        ResetAgent();
        
        //hasSpawnedBefore = true;
        hasDied = false;
    }
    private void ResetCoreState()
    {
        currentState = CharacterState.Idle;
        attackTarget = null;
        detectedTarget = null;
        isAttacking = false;
        isDead = false;
        hasWeapon = true;
    }
    private void ResetHealth()
    {
        if (IsServer && health != null)
        {
            health.CurrentHealth.Value = health.maxHealth;
        }
    }
    private void ResetUIAndCollider()
    {
        scoreDisplay.gameObject.SetActive(true);
        this.gameObject.layer = LayerMask.NameToLayer("Player");
        characterCollider.enabled = true;
    }
    private void ResetWeapon()
    {
        if (IsServer)
        {
            ChangeWeapon(weaponType);
        }
    }
    private void ResetAnimator()
    {
        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsAttacking", false);
        }

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
    }
    private void ResetAgent()
    {
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.ResetPath();
            agent.enabled = true; 
            agent.velocity = Vector3.zero;
        }
    }
    #endregion

    #region Weapons Handling
    public void ChangeWeapon(WeaponType newWeaponType)
    {
        if (!IsServer) return;

        ReleaseCurrentWeapon();

        WeaponBase weapon = ObjectPool.Instance.SpawnWeaponByOwner(this, newWeaponType);
        if (weapon == null) return;

        NetworkObject netObj = weapon.NetworkObj;
        SetupNewWeapon(weapon, netObj);
    }

    private void ReleaseCurrentWeapon()
    {
        if (currentWeapon == null) return;

        ObjectPool.Instance.ReleaseWeapon(currentWeapon.gameObject);
        currentWeapon.ClearOwner();
        currentWeapon = null;
    }

    private void SetupNewWeapon(WeaponBase weapon, NetworkObject netObj)
    {
        if (weapon == null || netObj == null) return;

        currentWeapon = weapon;
        currentWeapon.Init(this, weaponSpawnPoint);
        currentWeapon.SetOwner(this);

        if (!netObj.IsSpawned)
            netObj.Spawn(true);

        NetCurrentWeapon.Value = netObj;
        AssignWeapon(currentWeapon);
        SetWeaponClientRpc(netObj, this.networkObject);
    }

    private void HideOrReleaseWeapon()
    {
        if (currentWeapon == null) return;

        if (ownerType == OwnerType.Player)
        {
            currentWeapon.gameObject.SetActive(false);
        }
        else
        {
            if (IsServer)
                ObjectPool.Instance.ReleaseWeapon(currentWeapon.gameObject);
            else
                currentWeapon.gameObject.SetActive(false);
        }

        currentWeapon = null;
    }

    public void AssignWeapon(WeaponBase weapon)
    {
        currentWeapon = weapon;
        currentWeapon.Init(this, weaponSpawnPoint);
    }

    #endregion

    #region Death
    protected void CheckForDead()
    {
        if (health.IsDead && !hasDied)
        {
            hasDied = true;
            isDead = true;

            HandleDeathCleanup();
            HandleDeathAnimation();

            if (IsServer)
            {
                StartCoroutine(DeathSequenceCoroutine());
            }
        }
    }

    private IEnumerator DeathSequenceCoroutine()
    {

        float deathAnimTime = 1.0f; 
        yield return new WaitForSeconds(deathAnimTime);

        if (IsServer && GameManager.Instance != null)
        {
            GameManager.Instance.HandleCharacterDeath(this);

            SpawnCoinUniversal();
        }

        yield return new WaitForSeconds(0.1f);
        gameObject.SetActive(false);
    }

    private void HandleDeathCleanup()
    {
        StopAllCoroutines();
        scoreDisplay.gameObject.SetActive(false);
        characterCollider.enabled = true;

        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.enabled = false;
            agent.velocity = Vector3.zero;
            //agent.ResetPath();
        }
    }

    private void HandleDeathAnimationLocal()
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsAttacking", false);
            animator.CrossFade("Death", 0.1f);
        }
    }

    private void HandleDeathAnimation()
    {
        HandleDeathAnimationLocal(); 
        if (IsServer)
        {
            DisableColliderClientRpc();
            PlayDeathAnimationClientRpc();
        }
    }

    private void ReleaseWeaponOnDeath()
    {
        if (currentWeapon == null) return;
        
        HideOrReleaseWeapon();
        
        if (IsServer)
            ObjectPool.Instance.ReleaseWeapon(currentWeapon.gameObject);
        else
            currentWeapon.gameObject.SetActive(false);

        currentWeapon = null;
    }

    private IEnumerator DelayedDisable(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameObject.SetActive(false);
    }

    private void SpawnCoinUniversal()
    {
        GameObject coin = ObjectPool.Instance.SpawnCoin(transform.position + Vector3.up, Quaternion.identity);
        coin.SetActive(true);
    }

    #endregion
    
    #region Network Methods

    [ClientRpc]
    private void StopAttackAnimationClientRpc()
    {
        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
            animator.CrossFade("Idle", 0.05f);
        }
        
        isAttacking = false;
        hasWeapon = true;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        SetupWeaponSync();
        SetupInitialWeapon();
        SetupPlayerInfo();
        SetupScoreDisplay();
        
        SetupScoreSync();
        
        NetState.OnValueChanged += (oldVal, newVal) =>
        {
            currentState = newVal; // sync local state with server
        };
    }
    private void SetupWeaponSync()
    {
        NetCurrentWeapon.OnValueChanged += (oldVal, newVal) =>
        {
            if (newVal.TryGet(out NetworkObject obj))
            {
                currentWeapon = WeaponBase.GetByNetworkObject(obj);
                currentWeapon.SetOwner(this);
            }
            else
            {
                currentWeapon = null;
            }
        };

        StartCoroutine(ResolveCurrentWeaponInitial());
    }

    private void SetupInitialWeapon()
    {
        if (NetCurrentWeapon.Value.TryGet(out NetworkObject objNow))
        {
            currentWeapon = WeaponBase.GetByNetworkObject(objNow);
            currentWeapon.SetOwner(this);
        }

        if (IsOwner)
        {
            string savedWeapon = PlayerPrefs.GetString("SelectedWeapon", WeaponType.Knife1.ToString());

            if (!System.Enum.TryParse(savedWeapon, out WeaponType weaponType))
                weaponType = WeaponType.Knife1;

            RequestSetWeaponServerRpc(weaponType);
        }
    }

    private void SetupPlayerInfo()
    {
        if (IsOwner && ownerType == OwnerType.Player)
        {
            string localName = PlayerPrefs.GetString("PlayerName", "Player");
            SubmitPlayerNameServerRpc(localName);
        }
    }

    private void SetupScoreDisplay()
    {
        if (scoreDisplay != null)
        {
            scoreDisplay.SetScore(Score.Value);

            PlayerName.OnValueChanged += (oldName, newName) =>
            {
                scoreDisplay.SetPlayerName(newName.ToString());
            };

            scoreDisplay.SetPlayerName(PlayerName.Value.ToString());
        }
    }

    private void SetupScoreSync()
    {
        Score.OnValueChanged += OnScoreChanged;
        
        if (scoreDisplay != null)
        {
            scoreDisplay.SetScore(Score.Value);
            UpdateCharacterStats();
        }
    }

    private IEnumerator ResolveCurrentWeaponInitial()
    {
        yield return null;

        float timeout = 5f;
        float t = 0f;

        while (t < timeout && currentWeapon == null)
        {
            if (NetCurrentWeapon.Value.TryGet(out NetworkObject obj) && obj != null)
            {
                var weap = WeaponBase.GetByNetworkObject(obj);
                if (weap != null)
                {
                    currentWeapon = weap;
                    currentWeapon.SetOwner(this);
                    AssignWeapon(currentWeapon);
                    yield break;
                }
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }
        
        if (IsServer && currentWeapon == null)
        {
            ChangeWeapon(weaponType);
        }
    }
    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        
        Score.OnValueChanged -= OnScoreChanged;
        
        if (IsServer && currentWeapon != null)
        {
            ObjectPool.Instance.ReleaseWeapon(currentWeapon.gameObject);
            currentWeapon.ClearOwner();
   
        }
        
        if (IsOwner && ownerType == OwnerType.Player)
        {
            string localName = PlayerPrefs.GetString("PlayerName", "Player");
            SubmitPlayerNameServerRpc(localName);
        }
        Score.OnValueChanged -= (oldValue, newValue) =>
        {
            Debug.Log($"[CLIENT] {gameObject.name} Score synced {oldValue} -> {newValue}");
            scoreDisplay.SetScore(newValue);
            UpdateCharacterStats();
        };
    }
    [ServerRpc]
    private void SubmitPlayerNameServerRpc(string newName)
    {
        PlayerName.Value = new FixedString32Bytes(newName);
    }
    //Anim Attack

    [ServerRpc]
    public void RequestSetWeaponServerRpc(WeaponType selectedWeapon)
    {
        ChangeWeapon(selectedWeapon);
    }

    [ClientRpc]
    private void SetWeaponClientRpc(NetworkObjectReference weaponRef, NetworkObjectReference ownerRef)
    {
        if (weaponRef.TryGet(out NetworkObject weaponObj) &&
            ownerRef.TryGet(out NetworkObject ownerObj))
        {
            var weapon = WeaponBase.GetByNetworkObject(weaponObj);
            var ownerChar = ownerObj.TryGetComponent(out CharacterBase ch) ? ch : null;

            if (weapon != null && ownerChar != null)
            {
                ownerChar.AssignWeapon(weapon);
                weapon.SetOwner(ownerChar);
            }
        }
    }

    [ClientRpc]
    private void DisableColliderClientRpc()
    {
        if (characterCollider != null)
        {
            characterCollider.enabled = false;
        }
    }
    #endregion

    #region POWERUP

    [ServerRpc]
    public void RequestPickupPowerupServerRpc(NetworkObjectReference powerupRef, PowerupType type, float duration)
    {
        if (!powerupRef.TryGet(out NetworkObject powerupObj)) return;
        
        ApplyPowerupClientRpc(type, duration);
        
        powerupObj.Despawn();
    }

    // Sync OnTriggerEnter:  server call to sync for the clients
    [ClientRpc]
    public void ApplyPowerupClientRpc(PowerupType type, float duration)
    {
        if (!IsOwner && !IsHost) return;

        switch (type)
        {
            case PowerupType.SpeedBoost:
                StartCoroutine(ApplySpeedBoostLocal(duration));
                break;

            case PowerupType.WeaponGrow:
                StartCoroutine(ApplyWeaponGrowLocal(duration));
                break;
        }
    }

    private IEnumerator ApplySpeedBoostLocal(float duration, float multiplier = 2f)
    {
        if (isSpeedBoostActive)
        {
            StopCoroutine(speedBoostRoutine);
        }
        else
        {
            isSpeedBoostActive = true;
            moveSpeed = baseMoveSpeed * multiplier;
            if (agent != null) agent.speed = moveSpeed;
        }

        speedBoostRoutine = StartCoroutine(SpeedBoostTimer(duration));
        yield break;
    }

    private IEnumerator SpeedBoostTimer(float duration)
    {
        yield return new WaitForSeconds(duration);
        
        moveSpeed = baseMoveSpeed;
        if (agent != null) agent.speed = moveSpeed;

        isSpeedBoostActive = false;
        speedBoostRoutine = null;
    }
    
    private IEnumerator ApplyWeaponGrowLocal(float duration, float scaleMultiplier = 1.5f, float speedMultiplier = 1.5f)
    {
        if (currentWeaponPublic == null) yield break;
        WeaponBase weapon = currentWeaponPublic;

        if (isWeaponGrowActive)
        {
            StopCoroutine(weaponGrowRoutine);
        }
        else
        {
            isWeaponGrowActive = true;

            weapon.BuffScaleMultiplier = scaleMultiplier;
            weapon.speed = weapon.OriginalSpeed * speedMultiplier;
            
            weapon.ApplyScale();
        }

        weaponGrowRoutine = StartCoroutine(WeaponGrowTimer(duration));
    }


    private IEnumerator WeaponGrowTimer(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (currentWeaponPublic != null)
        {
            WeaponBase weapon = currentWeaponPublic;
            weapon.BuffScaleMultiplier = 1f;
            weapon.speed = weapon.OriginalSpeed;
            
            weapon.ApplyScale();
        }

        isWeaponGrowActive = false;
        weaponGrowRoutine = null;
    }
   
    public void ApplyPowerupLocal(PowerupType type, float duration)
    {
        switch (type)
        {
            case PowerupType.SpeedBoost:
                StartCoroutine(ApplySpeedBoostLocal(duration));
                break;

            case PowerupType.WeaponGrow:
                StartCoroutine(ApplyWeaponGrowLocal(duration));
                break;
        }
    }

    #endregion
    
    #region Attack Animation Event
    [ServerRpc]
    private void RequestLaunchServerRpc(ServerRpcParams rpcParams = default)
    {
        if (isDead || currentWeapon == null || attackTarget == null)
        {
            return;
        }
        
        if (currentState != CharacterState.Attack) return;
        Vector3 dir = (attackTarget.position - weaponSpawnPoint.position).normalized;
        Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(weaponRotationOffset);

        currentWeapon.transform.rotation = rot;
        currentWeapon.Launch(dir, this.gameObject);

        LaunchWeaponClientRpc(dir, rot);
    }

    [ClientRpc]
    private void LaunchWeaponClientRpc(Vector3 dir, Quaternion rot, ClientRpcParams rpcParams = default)
    {
        if (!isActiveAndEnabled) return;
        StartCoroutine(WaitUntilWeaponReady(dir, rot));
    }
    private IEnumerator WaitUntilWeaponReady(Vector3 dir, Quaternion rot)
    {
        currentWeapon.transform.rotation = rot;
        
        //shoot real weapon
        currentWeapon.Launch(dir, this.gameObject);
        yield return new WaitUntil(() => currentWeapon != null);
    }
    [ServerRpc]
    public void RequestChangeWeaponServerRpc(WeaponType newWeaponType)
    {
        ChangeWeapon(newWeaponType);
    }
    [ClientRpc]
    private void PlayDeathAnimationClientRpc()
    {
        HandleDeathAnimationLocal();
    }

    #endregion

    #region Coin
    [ClientRpc]
    private void AddCoinClientRpc(int amount, ClientRpcParams rpcParams = default)
    {
        CoinManager.Instance.AddCoin(amount);
    }
    public void AddCoinToClient(ulong clientId, int amount)
    {
        AddCoinClientRpc(amount, new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            }
        });
    }

    #endregion
}