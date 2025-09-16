using DG.Tweening;
using System.Collections;
using Unity.Netcode;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Splines;
using static UnityEngine.Rendering.DebugUI.Table;

public enum CharacterState { Idle, Move, Attack }

public abstract class CharacterBase : NetworkBehaviour
{
    [Header("Character Settings")]
    [SerializeField] public float moveSpeed = 5f;
    [SerializeField] public NavMeshAgent agent;
    [SerializeField] protected Animator animator;
    [SerializeField] public Health health;
    [SerializeField] protected float attackRange = 2f;
    [SerializeField] protected float attackDuration = 0.5f;
    [SerializeField] protected LayerMask targetLayer;
    [SerializeField] public Collider characterCollider;

    [Header("Weapon Settings")]
    [SerializeField] public Transform weaponSpawnPoint;
    [SerializeField] protected Vector3 weaponRotationOffset = Vector3.zero;
    [SerializeField] protected float attackDelay = 0.3f;
    [SerializeField] public WeaponType weaponType;
    protected WeaponBase currentWeapon;

    [Header("Score Settings")]
    [SerializeField] private KillScoreDisplay scoreDisplay;
    [SerializeField] private float sizePerScore = 0.05f;
    [SerializeField] private float rangePerScore = 0.1f;
    [SerializeField] private float moveSpeedPerScore = 0.1f;
    [SerializeField] private float maxScale = 3f;

    [Header("Default Weapon")]
    [SerializeField] private WeaponType defaultWeapon = WeaponType.Knife;

    protected CharacterState currentState = CharacterState.Idle;
    protected Transform attackTarget;
    protected Transform detectedTarget;
    protected bool isAttacking = false;
    protected bool hasWeapon = true;
    protected float nextAttackTime = 0f;
    protected bool isDead = false;
    private Vector3 lastPosition;
    private Coroutine attackRoutine;


    public float currentAttackRange => attackRange;
    public WeaponBase currentWeaponPublic => currentWeapon;

    public NetworkVariable<int> Score = new NetworkVariable<int>(
    0, NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> NetIsMoving = new NetworkVariable<bool>(
    false,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Owner);

    protected virtual void Start()
    {
        if (agent != null)
        {
            agent.speed = moveSpeed;
            lastPosition = transform.position;
        }

        //if (IsServer)
        //{
        //    RequestSetWeaponServerRpc(WeaponType.Knife);
        //}
        OnWeaponReturned();
    }

    protected virtual void Update()
    {
        if (!GameManager.Instance || !GameManager.Instance.IsGameStarted)
            return;
        if (IsClient && IsOwner)
        {
            Debug.Log($"{name} [Client] Update - State: {currentState}, AttackTarget: {attackTarget?.name}");
        }
        CheckForDead();
        if (isDead || health.IsDead)
        {
            return;
        }
        if (agent == null || !agent.isActiveAndEnabled)
        {
            return;
        }

        UpdateRadar();

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
                EndAttack();
                ChangeState(CharacterState.Idle);
            }
        }
    }

    protected virtual void OnNewTargetFound(Transform newTarget) { }

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

    protected void ChangeState(CharacterState newState)
    {
        if (newState == CharacterState.Attack && currentState == CharacterState.Move)
        {
            Debug.Log($"{name} đang di chuyển, bỏ qua yêu cầu chuyển sang Attack");
            return;
        }
        if (currentState == newState) return;

        if (currentState == CharacterState.Attack)
        {
            if (animator != null)
            {
                animator.SetBool("IsAttacking", false);
            }
            EndAttack();
        }
        currentState = newState;
    }

    protected virtual void HandleIdle()
    {
        Vector3 input = GetMovementInput();
        if (input.magnitude > 0.01f)
        {
            ChangeState(CharacterState.Move);
        }
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
        CheckForAttack();
    }

    protected virtual void HandleAttack()
    {
        if (attackTarget == null || Vector3.Distance(transform.position, attackTarget.position) > attackRange)
        {
            EndAttack();
            return;
        }

        FaceTarget(attackTarget.position);
        if (!isAttacking)
        {
            PerformAttack();
        }
    }

    protected virtual void CheckForAttack()
    {
        if (currentState == CharacterState.Move) return; 

        if (this is Player player && player.isMovingInput)
        {
            return;
        }

        if (attackTarget != null && attackTarget != detectedTarget)
        {
            float distanceToAttackTarget = Vector3.Distance(transform.position, attackTarget.position);
            if (distanceToAttackTarget > attackRange || !attackTarget.gameObject.activeInHierarchy)
            {
                attackTarget = null;
                EndAttack();
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
        Debug.Log($"{name} PerformAttack: isAttacking={isAttacking}, hasWeapon={hasWeapon}, currentWeapon={currentWeapon?.name}");
        if (currentState != CharacterState.Idle && currentState != CharacterState.Attack)
        {
            return;
        }
        if (currentWeapon == null)
            return;
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
        attackRoutine = StartCoroutine(AttackRoutine()); Debug.Log($"{name} PerformAttack: isAttacking={isAttacking}, hasWeapon={hasWeapon}, currentWeapon={currentWeapon?.name}");
    }

    private IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(attackDelay);
        if (IsOwner)  // chỉ owner gửi request lên server
        {
            RequestLaunchServerRpc();
        }
        else
        {
            Debug.Log($"{name} không phải Owner, không gửi yêu cầu bắn");
        }
        yield return new WaitForSeconds(0.1f);

        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
        }
    }



    public virtual void OnWeaponReturned()
    {
        Debug.Log($"{name} nhận OnWeaponReturned từ {currentWeapon?.name}");
        isAttacking = false;
        hasWeapon = true;
    }

    protected virtual void ThrowWeapon()
    {
        if (currentWeapon != null && attackTarget != null)
        {
            Vector3 dir = (attackTarget.position - weaponSpawnPoint.position).normalized;
            Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(weaponRotationOffset);
            currentWeapon.transform.rotation = rot;

            if (IsServer)
            {
                currentWeapon.Launch(dir, this.gameObject);

                //Send event launch to all the different clients
                LaunchWeaponClientRPC(dir);
            }
        }
    }

    protected virtual void EndAttack()
    {
        Debug.Log($"{name} EndAttack: reset trạng thái, vũ khí={(currentWeapon != null ? currentWeapon.name : "null")}");
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        isAttacking = false;
        hasWeapon = true;

        if (currentWeapon != null && !currentWeapon.IsFlying)
        {
            currentWeapon.ResetWeapon();
        }

        nextAttackTime = Time.time;

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
        if (animator == null || agent == null || !agent.isActiveAndEnabled) return;

        bool isMovingNow = agent.velocity.sqrMagnitude > 0.01f && !isAttacking;
        animator.SetBool("IsMoving", isMovingNow);
    }


    public abstract Vector3 GetMovementInput();

    public void AddScore(int value)
    {
        if (!IsServer) return;
        Score.Value += value;
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
        scoreDisplay.gameObject.SetActive(true);
        this.gameObject.layer = LayerMask.NameToLayer("Enemy");
        characterCollider.enabled = true;
        if (currentWeapon != null)
        {
            //currentWeapon.transform.SetParent(weaponSpawnPoint);
            currentWeapon.transform.localPosition = Vector3.zero;
            currentWeapon.transform.localRotation = Quaternion.identity;
            currentWeapon.gameObject.SetActive(true);
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

    }

    public void ChangeWeapon(WeaponType newWeaponType)
    {
        if (!IsServer) return; // ❗ Client không spawn

        if (currentWeapon != null)
        {
            ObjectPool.Instance.ReleaseWeapon(currentWeapon.gameObject);
            currentWeapon = null;
        }

        var go = ObjectPool.Instance.SpawnWeaponByType(newWeaponType);
        if (go == null) return;

        currentWeapon = go.GetComponent<WeaponBase>();
        currentWeapon.transform.position = weaponSpawnPoint.position;
        currentWeapon.transform.rotation = weaponSpawnPoint.rotation;

        var netObj = currentWeapon.GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned)
        {
            netObj.Spawn(true);
            Debug.Log($"{name} [Server] SpawnWeapon {currentWeapon.name}");

            // Gửi reference xuống client
            SetWeaponClientRpc(netObj);
        }

        currentWeapon.Init(this, weaponSpawnPoint);
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

    protected virtual void CheckForDead()
    {
        if (health.IsDead == true)
        {
            StopAllCoroutines();
            scoreDisplay.gameObject.SetActive(false);
            //characterCollider.enabled = false;
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.ResetPath();
            }
            
            isDead = true;
        }

        //if (currentWeapon != null)
        //{
        //    if (IsServer)
        //    {
        //        ObjectPool.Instance.ReleaseWeapon(currentWeapon.gameObject); 
        //    }
        //    else
        //    {
        //        currentWeapon.gameObject.SetActive(false);
        //    }
        //    currentWeapon = null;
        //}
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
    public void AssignWeapon(WeaponBase weapon)
    {
        currentWeapon = weapon;
        currentWeapon.Init(this, weaponSpawnPoint);
        Debug.Log($"{name} AssignWeapon -> currentWeapon={weapon.name}");
    }

    #region Network Methods
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsOwner)
        {
            string savedWeapon = PlayerPrefs.GetString("SelectedWeapon", WeaponType.Knife.ToString());

            if (!System.Enum.TryParse(savedWeapon, out WeaponType weaponType))
                weaponType = WeaponType.Knife;

            RequestSetWeaponServerRpc(weaponType);
        }



        if (scoreDisplay != null)
        {
            scoreDisplay.SetScore(Score.Value);

            Score.OnValueChanged += (oldValue, newValue) =>
            {
                scoreDisplay.SetScore(newValue);
                UpdateCharacterStats();
            };
        }

        NetIsMoving.OnValueChanged += (oldVal, newVal) =>
        {
            if (animator != null)
            {
                animator.SetBool("IsMoving", newVal);
            }
        };


    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        Score.OnValueChanged -= (oldValue, newValue) =>
        {
            scoreDisplay.SetScore(newValue);
            UpdateCharacterStats();
        };
    }

    //Anim Attack
    [ServerRpc]
    public void RequestAttackServerRpc()
    {
        Debug.Log($"{name} [Server] nhận RequestAttackServerRpc từ ClientId={OwnerClientId}, State={currentState}, isDead={isDead}");

        if (isDead || currentState != CharacterState.Idle) return;

        if (!isAttacking && hasWeapon)
        {
            Debug.Log($"{name} [Server] hợp lệ để Attack => gọi PerformAttack()");
            PerformAttack();
            PlayAttackAnimationClientRpc();
        }

        else
        {
            Debug.LogWarning($"{name} [Server] không hợp lệ để Attack: isAttacking={isAttacking}, hasWeapon={hasWeapon}");
        }
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

        //// Gửi vũ khí về client
        //SetWeaponClientRpc(selectedWeapon);
    }

    [ClientRpc]
    private void SetWeaponClientRpc(NetworkObjectReference weaponRef)
    {
        if (weaponRef.TryGet(out NetworkObject weaponObj))
        {
            currentWeapon = weaponObj.GetComponent<WeaponBase>();
            if (currentWeapon != null)
            {
                currentWeapon.Init(this, weaponSpawnPoint); 
            }
        }
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


    #endregion

    #region POWERUP

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
        Debug.Log($"{name} [Server] nhận RequestLaunchServerRpc");
        if (isDead || currentWeapon == null || attackTarget == null)
        {
            Debug.LogWarning($"{name} [Server] không thể Launch: chết={isDead}, weapon={(currentWeapon == null)}, target={(attackTarget == null)}");
            return;
        }
        Vector3 dir = (attackTarget.position - weaponSpawnPoint.position).normalized;
        Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(weaponRotationOffset);

        Debug.Log($"{name} [Server] bắn vũ khí về hướng: {dir}");

        currentWeapon.transform.rotation = rot;
        // Server bắn
        currentWeapon.Launch(dir, this.gameObject);

        // Sync cho toàn bộ client
        LaunchWeaponClientRpc(dir, rot);
    }

    [ClientRpc]
    private void LaunchWeaponClientRpc(Vector3 dir, Quaternion rot, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"{name} [Client] nhận LaunchWeaponClientRpc từ server");

        if (currentWeapon != null)
        {
            currentWeapon.transform.rotation = rot;
            currentWeapon.Launch(dir, this.gameObject);
            Debug.Log($"{name} [Client] bắt đầu Launch vũ khí với hướng: {dir}");
        }
        else
        {
            Debug.LogWarning($"{name} [Client] KHÔNG có vũ khí để bắn!");
        }
    }

    [ServerRpc]
    public void RequestChangeWeaponServerRpc(WeaponType newWeaponType)
    {
        ChangeWeapon(newWeaponType); // Server trực tiếp spawn vũ khí
    }

    #endregion
}