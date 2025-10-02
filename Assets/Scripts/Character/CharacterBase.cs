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
    [SerializeField] private WeaponType defaultWeapon = WeaponType.Knife;

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
    protected bool isDead = false;
    private Vector3 lastPosition;
    private Coroutine attackRoutine;
    private bool hasSpawnedBefore = false;
    private bool queuedMove = false;

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

    protected virtual void Start()
    {
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
        if (currentWeapon != null)
        {
            //currentWeapon.gameObject.SetActive(false);
        }
        attackTarget = null;
        detectedTarget = null;
    }
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
    
    #endregion

    #region Movement
    protected bool IsMovingNow()
    {
        if (this is Player player)
        {
            return player.isMovingInput || player.NetIsMoving.Value;
        }

        if (agent != null && agent.isActiveAndEnabled)
        {
            return agent.velocity.magnitude > 0.05f;
        }

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

        bool isMovingNow;

        if (IsOwner) 
        {
            // Local player
            isMovingNow = this is Player player ? player.isMovingInput : speed > 0.01f;
        }
        else
        {
            // Remote/AI → sync server
            isMovingNow = NetIsMoving.Value;
        }

        animator.SetBool("IsMoving", isMovingNow);

        bool attackingNow = IsOwner ? isAttacking : NetIsAttacking.Value;
        animator.SetBool("IsAttacking", isAttacking);

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
        Collider[] targets = Physics.OverlapSphere(transform.position, attackRange);

        Transform nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (var target in targets)
        {

            if (target.transform == transform || !target.gameObject.activeInHierarchy)
                continue;

            if (!target.CompareTag("Player")) continue;

            CharacterBase otherChar = target.GetComponent<CharacterBase>();
            if (otherChar == null || otherChar == this) continue;

            float distance = Vector3.Distance(transform.position, otherChar.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = otherChar.transform;
            }
        }

        return nearest;
    }
    #endregion
    
    #region State Handling
    protected void ChangeState(CharacterState newState)
    {
        if (currentState == newState) return;

        if (currentState == CharacterState.Attack)
        {
            if (animator != null)
            {
                animator.SetBool("IsAttacking", false);
            }
        }
        currentState = newState;
        
        if (IsServer) 
            NetState.Value = newState;
        
        if (currentState == CharacterState.Attack)
        {
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
        }
    }

    protected virtual void HandleIdle()
    {
        Vector3 input = GetMovementInput();
        if (input.magnitude > 0.01f)
        {
            ChangeState(CharacterState.Move);
        }
        else
            CheckForAttack();
    }

    protected virtual void HandleMove()
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

    protected virtual void HandleAttack()
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
        {
            PerformAttack();
        }

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
        if (IsMovingNow()) return;
        if (currentState == CharacterState.Move) return;

        if (this is Player player && player.isMovingInput)
        {
            if (player.NetIsMoving.Value)
                return;
        }

        if (attackTarget != null && attackTarget != detectedTarget)
        {
            float distanceToAttackTarget = Vector3.Distance(transform.position, attackTarget.position);
            if (distanceToAttackTarget > attackRange || !attackTarget.gameObject.activeInHierarchy)
            {
                attackTarget = null;
                EndAttack(true);
                ChangeState(CharacterState.Idle);
            }
        }

        if (detectedTarget != null && Vector3.Distance(transform.position, detectedTarget.position) <= attackRange)
        {
            if (currentState != CharacterState.Attack || attackTarget != detectedTarget)
            {
                attackTarget = detectedTarget;
                ChangeState(CharacterState.Attack);
            }
        }
    }

    protected virtual void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 30f);
        }
    }

    protected virtual void PerformAttack()
    {
        if (currentState != CharacterState.Idle && currentState != CharacterState.Attack)
        {
            return;
        }

        if (agent != null && agent.isActiveAndEnabled && agent.velocity.magnitude > 0.01f)
        {
            return;
        }

        if (currentWeapon == null)
        {
            StartCoroutine(WaitWeaponAndAttack());
            Debug.Log("Current weapon is null, equipping default weapon.");
            return;
        }
        if (!hasWeapon)
        {
            return;
        }
        if (currentWeapon.IsFlying)
        {
            return;
        }
        if (Time.time < nextAttackTime)
        {
            return;
        }

        isAttacking = true;
        if (IsServer) NetIsAttacking.Value = true;
        hasWeapon = false;
        nextAttackTime = Time.time + attackDelay;

        if (animator != null)
        {
            animator.SetBool("IsAttacking", true);
        }

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
        }
        attackRoutine = StartCoroutine(AttackRoutine());
    }
    private IEnumerator WaitWeaponAndAttack()
    {
        yield return new WaitUntil(() => currentWeapon != null);
        DoAttack();
    }
    private void DoAttack()
    {
        isAttacking = true;
        if (IsServer) NetIsAttacking.Value = true;
        hasWeapon = false;
        nextAttackTime = Time.time + attackDelay;

        if (animator != null)
            animator.SetBool("IsAttacking", true);

        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        attackRoutine = StartCoroutine(AttackRoutine());
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


    public virtual void OnWeaponReturned()
    {
        isAttacking = false;
        hasWeapon = true;
    }

    

    protected virtual void EndAttack(bool cancelByMove = false)
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        isAttacking = false;
        hasWeapon = true;
        if (IsServer) NetIsAttacking.Value = false;

        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
            
            if (cancelByMove)
                animator.CrossFade("Run", 0.1f); 
            else
                animator.CrossFade("Idle", 0.1f);
        }

        if (currentWeapon != null && !currentWeapon.IsFlying)
        {
            currentWeapon.ResetWeapon();
        }

        if (agent != null && agent.isActiveAndEnabled && !isDead)
        {
            agent.isStopped = false;
        }

        nextAttackTime = Time.time;
        
        if (IsMovingNow()) 
            ChangeState(CharacterState.Move);
        else
            ChangeState(CharacterState.Idle);
    }

    #endregion

    #region  Score & Stats
    private void OnScoreChanged(int oldValue, int newValue)
    {
        Debug.Log($"[CLIENT] {gameObject.name} Score synced {oldValue} -> {newValue}");
        if (scoreDisplay != null)
        {
            scoreDisplay.SetScore(newValue);
            UpdateCharacterStats();
        }
    }

    public void AddScore(int value)
    {
        if (!IsServer) return;
        Score.Value += value;
        
        Debug.Log($"[SERVER] {gameObject.name} Score = {Score.Value}");
    }

    private void UpdateCharacterStats()
    {
        if (scoreDisplay == null) return;

        float newScale = Mathf.Min(1f + scoreDisplay.CurrentScore * sizePerScore, maxScale);
        transform.localScale = Vector3.one * newScale;

        if (currentWeapon != null)
        {
            currentWeapon.transform.localScale = Vector3.one * newScale;
        }

        attackRange += scoreDisplay.CurrentScore * rangePerScore;
        moveSpeed += moveSpeedPerScore;

        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.speed = moveSpeed;
        }
    }

    public virtual void ResetState()
    {

        currentState = CharacterState.Idle;
        attackTarget = null;
        detectedTarget = null;
        isAttacking = false;
        isDead = false;
        hasWeapon = true;

        if (IsServer && health != null)
        {
            health.CurrentHealth.Value = health.maxHealth;
        }

        scoreDisplay.gameObject.SetActive(true);
        this.gameObject.layer = LayerMask.NameToLayer("Player");
        characterCollider.enabled = true;

        if (IsServer)
        {
            ChangeWeapon(weaponType);
        }

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

        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }

        hasSpawnedBefore = true;
    }

    #endregion

    #region Weapons Handling
    public void ChangeWeapon(WeaponType newWeaponType)
    {
        if (!IsServer) return;

        if (currentWeapon != null)
        {
            ObjectPool.Instance.ReleaseWeapon(currentWeapon.gameObject);
            currentWeapon.ClearOwner();
            currentWeapon = null;
        }

        GameObject go = null;

        if (ownerType == OwnerType.Player)
        {
            go = ObjectPool.Instance.SpawnPlayerWeaponByType(newWeaponType, weaponSpawnPoint, true);
        }
        else if (ownerType == OwnerType.AI)
        {
            go = ObjectPool.Instance.SpawnAIWeaponByType(newWeaponType, weaponSpawnPoint, true);
        }

        if (go == null) return;

        currentWeapon = go.GetComponent<WeaponBase>();
        currentWeapon.Init(this, weaponSpawnPoint);
        currentWeapon.SetOwner(this);

        var netObj = currentWeapon.GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned)
        {
            netObj.Spawn(true);
        }

        NetCurrentWeapon.Value = netObj;
        AssignWeapon(currentWeapon);
        SetWeaponClientRpc(netObj, this.networkObject);

    }
    
    protected virtual void LoadWeapon()
    {
        string selectedWeaponName = PlayerPrefs.GetString("SelectedWeapon", "");
        if (!string.IsNullOrEmpty(selectedWeaponName))
        {
            foreach (WeaponData weapon in Resources.LoadAll<WeaponData>(""))
            {
                if (weapon.weaponName == selectedWeaponName)
                {
                    ChangeWeapon(weapon.weaponType);
                    break;
                }
            }
        }
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
    protected virtual void CheckForDead()
    {
        if (health.IsDead == true)
        {
            isDead = true;

            if (IsServer)
            {
                GameManager.Instance.UnregisterAI(this.networkObject);
            }

            StopAllCoroutines();
            scoreDisplay.gameObject.SetActive(false);
            characterCollider.enabled = false;
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.ResetPath();
            }

            if (animator != null)
            {
                animator.SetBool("IsMoving", false);
                animator.SetBool("IsAttacking", false);
            }

            if (IsServer)
            {
                DisableColliderClientRpc();
                PlayDeathAnimationClientRpc();
            }

            if (IsServer)
            {
                PlayDeathAnimationClientRpc();
            }

            if (currentWeapon != null)
            {
                if (IsServer)
                {
                    ObjectPool.Instance.ReleaseWeapon(currentWeapon.gameObject);
                }
                else
                {
                    currentWeapon.gameObject.SetActive(false);
                }
                currentWeapon = null;
            }

            HideOrReleaseWeapon();

            StartCoroutine(DelayedDisable(1.5f));
        }
    }
    
    private IEnumerator DelayedDisable(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameObject.SetActive(false);
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
                currentWeapon = obj.GetComponent<WeaponBase>();
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
            currentWeapon = objNow.GetComponent<WeaponBase>();
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
                var weap = obj.GetComponent<WeaponBase>();
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

    private IEnumerator StopAttackAnimDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        StopAttackAnimationClientRpc();
    }

    [ClientRpc]
    public void PlayAttackAnimationClientRpc()
    {
        if (animator != null)
        {
            animator.SetBool("IsAttacking", true);
        }
    }

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
            var weapon = weaponObj.GetComponent<WeaponBase>();
            var ownerChar = ownerObj.GetComponent<CharacterBase>();

            if (weapon != null && ownerChar != null)
            {
                ownerChar.AssignWeapon(weapon);
                weapon.SetOwner(ownerChar);
            }
        }
    }

    [ServerRpc]
    private void RequestStopAttackAnimationServerRpc()
    {
        StopAttackAnimationClientRpc();
    }
    [ClientRpc]
    private void LaunchWeaponClientRPC(Vector3 dir)
    {
        if (IsServer) return;
        if (currentWeapon != null)
        {
            currentWeapon.Launch(dir, this.gameObject);
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
        float oldSpeed = moveSpeed;
        moveSpeed *= multiplier;
        if (agent != null) agent.speed = moveSpeed;

        yield return new WaitForSeconds(duration);

        moveSpeed = oldSpeed;
        if (agent != null) agent.speed = moveSpeed;
    }

    private IEnumerator ApplyWeaponGrowLocal(float duration, float scaleMultiplier = 1.5f)
    {
        if (currentWeaponPublic == null) yield break;

        Transform weaponTransform = currentWeaponPublic.transform;
        Vector3 oldScale = weaponTransform.localScale;

        weaponTransform.localScale = oldScale * scaleMultiplier;

        yield return new WaitForSeconds(duration);

        if (currentWeaponPublic != null)
            weaponTransform.localScale = oldScale;
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
        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
            animator.SetBool("IsAttacking", false);
        }
    }

    #endregion
}