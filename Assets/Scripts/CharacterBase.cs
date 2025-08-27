using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public enum CharacterState
{
    Idle,
    Move,
    Attack
}

public abstract class CharacterBase : MonoBehaviour
{
    [Header("Character Settings")]
    [SerializeField] protected float moveSpeed = 5f;
    [SerializeField] protected NavMeshAgent agent;
    [SerializeField] protected Animator animator;
    [SerializeField] protected float attackRange = 2f;
    [SerializeField] protected float attackDuration = 0.5f;
    [SerializeField] protected LayerMask targetLayer;
    [SerializeField] protected float detectionRange = 10f;

    [Header("Weapon Settings")]
    [SerializeField] protected Transform weaponSpawnPoint;
    [SerializeField] protected Vector3 weaponRotationOffset = Vector3.zero;
    [SerializeField] protected float attackDelay = 0.3f;
    [SerializeField] protected WeaponType weaponType;
    protected WeaponBase currentWeapon;

    [Header("Score Settings")]
    [SerializeField] private KillScoreDisplay scoreDisplay;
    [SerializeField] private float sizePerScore = 0.05f;
    [SerializeField] private float rangePerScore = 0.1f;
    [SerializeField] private float moveSpeedPerScore = 0.1f;
    [SerializeField] private float maxScale = 3f;

    protected CharacterState currentState = CharacterState.Idle;
    protected Transform attackTarget;
    protected Transform detectedTarget;
    protected bool isAttacking = false;
    protected bool isMoving = false;
    private Coroutine attackRoutine;
    protected bool hasWeapon = true;

    public float currentAttackRange => attackRange;
    public WeaponBase currentWeaponPublic => currentWeapon;

    protected virtual void Start()
    {
        InitializeAgent();
        InitializeWeapon();
        OnWeaponReturned();
        LoadWeapon();
    }

    private void InitializeAgent()
    {
        if (agent != null)
        {
            agent.speed = moveSpeed;
        }
    }

    private void InitializeWeapon()
    {
        if (weaponSpawnPoint != null)
        {
            GameObject weaponObj = ObjectPool.Instance.SpawnWeaponByType(weaponType);
            if (weaponObj != null)
            {
                weaponObj.transform.SetParent(weaponSpawnPoint);
                weaponObj.transform.localPosition = Vector3.zero;
                weaponObj.transform.localRotation = Quaternion.identity;

                currentWeapon = weaponObj.GetComponent<WeaponBase>();
                if (currentWeapon != null)
                {
                    currentWeapon.Init(this, weaponSpawnPoint);
                }
            }
        }
    }

    protected virtual void Update()
    {
        if (agent == null || !agent.isActiveAndEnabled) return;

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

    protected virtual void OnNewTargetFound(Transform newTarget)
    {
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
        Collider[] targets = Physics.OverlapSphere(transform.position, detectionRange, targetLayer);
        Transform nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (var target in targets)
        {
            if (target.transform == transform || !target.gameObject.activeInHierarchy)
                continue;

            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = target.transform;
            }
        }

        return nearest;
    }

    protected void ChangeState(CharacterState newState)
    {
        if (currentState != newState)
        {
            if (currentState == CharacterState.Attack && newState == CharacterState.Move)
            {
                if (animator != null)
                {
                    animator.SetBool("IsAttacking", false);
                }
                EndAttack();
            }
            currentState = newState;
        }
    }

    protected virtual void HandleIdle()
    {
        Vector3 input = GetMovementInput();
        if (input.magnitude > 0.1f)
        {
            ChangeState(CharacterState.Move);
        }
        CheckForAttack();
    }

    protected virtual void HandleMove()
    {
        Vector3 input = GetMovementInput();
        if (input.magnitude > 0.1f)
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
        if (currentWeapon == null || !hasWeapon) return;
        if (currentWeapon.IsFlying) return;

        isAttacking = true;
        hasWeapon = false;

        if (animator != null)
            animator.SetBool("IsAttacking", true);

        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        attackRoutine = StartCoroutine(AttackRoutine());
        DOVirtual.DelayedCall(attackDuration, () =>
        {
            if (animator != null)
                animator.SetBool("IsAttacking", false);
        });
    }

    private IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(attackDelay);

        if (currentWeapon != null && attackTarget != null)
            ThrowWeapon();
    }

    public virtual void OnWeaponReturned()
    {
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
            currentWeapon.Launch(dir, this.gameObject);
        }
    }

    protected virtual void EndAttack()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        isAttacking = false;
        if (currentWeapon != null && !currentWeapon.IsFlying)
        {
            currentWeapon.ResetWeapon();
        }
        isAttacking = false;
        hasWeapon = true;
    }

    protected virtual void Move(Vector3 direction)
    {
        if (isAttacking || agent == null || !agent.isActiveAndEnabled) return;
        direction = direction.normalized;
        Vector3 targetPos = transform.position + direction * 2f;

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
    }

    protected virtual void UpdateAnimator()
    {
        if (animator == null)
            return;

        bool agentIsMoving = agent != null && agent.isActiveAndEnabled && agent.velocity.magnitude > 0.1f;
        isMoving = agentIsMoving && !isAttacking;

        animator.SetBool("IsMoving", isMoving);
        if (isMoving)
        {
            animator.SetBool(("IsAttacking"), false);
        }
    }

    public abstract Vector3 GetMovementInput();

    public void AddScore(int value)
    {
        if (scoreDisplay != null)
        {
            scoreDisplay.SetScore(value);
            UpdateCharacterStats();
        }
    }

    private void UpdateCharacterStats()
    {
        if (scoreDisplay == null)
            return;
        float newScale = Mathf.Min(1f + scoreDisplay.CurrentScore * sizePerScore, maxScale);
        transform.localScale = Vector3.one * newScale;
        currentWeapon.transform.localScale = Vector3.one * newScale;
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
        isMoving = false;

        if (currentWeapon != null)
        {
            currentWeapon.transform.SetParent(weaponSpawnPoint);
            currentWeapon.transform.localPosition = Vector3.zero;
            currentWeapon.transform.localRotation = Quaternion.identity;
            currentWeapon.gameObject.SetActive(true);
        }

        hasWeapon = true;

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
        // Xóa weapon cũ
        if (currentWeapon != null)
        {
            currentWeapon.gameObject.SetActive(false);
        }

        // Spawn weapon mới từ pool
        GameObject newWeaponObj = ObjectPool.Instance.SpawnWeaponByType(newWeaponType);
        if (newWeaponObj != null)
        {
            newWeaponObj.transform.SetParent(weaponSpawnPoint);
            newWeaponObj.transform.localPosition = Vector3.zero;
            newWeaponObj.transform.localRotation = Quaternion.identity;

            currentWeapon = newWeaponObj.GetComponent<WeaponBase>();
            if (currentWeapon != null)
            {
                currentWeapon.Init(this, weaponSpawnPoint);
            }
        }

        weaponType = newWeaponType;
    }

    protected virtual void OnDisable()
    {
        if (currentWeapon != null)
        {
            currentWeapon.gameObject.SetActive(false);
        }
        attackTarget = null;
        detectedTarget = null;
    }
    protected virtual void LoadWeapon()
    {
        string selectedWeaponName = PlayerPrefs.GetString("SelectedWeapon", "");
        if (!string.IsNullOrEmpty(selectedWeaponName))
        {
            foreach (WeaponData weapon in Resources.LoadAll<WeaponData>("")) // hoặc gắn list weapons
            {
                if (weapon.weaponName == selectedWeaponName)
                {
                    ChangeWeapon(weapon.weaponType);
                    break;
                }
            }
        }
    }
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}