using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public enum CharacterState { Idle, Move, Attack }

public abstract class CharacterBase : MonoBehaviour
{
    [Header("Character Settings")]
    [SerializeField] protected float moveSpeed = 5f;
    [SerializeField] protected NavMeshAgent agent;
    [SerializeField] protected Animator animator;
    [SerializeField] protected Health health;
    [SerializeField] protected float attackRange = 2f;
    [SerializeField] protected float attackDuration = 0.5f;
    [SerializeField] protected LayerMask targetLayer;
    [SerializeField] protected Collider characterCollider;

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
    protected bool hasWeapon = true;
    protected float nextAttackTime = 0f;
    protected bool isDead = false;
    private Vector3 lastPosition;
    private Coroutine attackRoutine;

    public float currentAttackRange => attackRange;
    public WeaponBase currentWeaponPublic => currentWeapon;

    protected virtual void Start()
    {
        if (agent != null)
        {
            agent.speed = moveSpeed;
            lastPosition = transform.position;
        }
        InitializeWeapon();
        OnWeaponReturned();
        LoadWeapon();
    }

    private void InitializeWeapon()
    {
        if (weaponSpawnPoint == null) return;

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

    protected virtual void Update()
    {
        CheckForDead();
        if (isDead || health.isDead)
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
        Collider[] targets = Physics.OverlapSphere(transform.position, attackRange, targetLayer);
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
        if (!hasWeapon)
        {
            DOVirtual.DelayedCall(attackDuration, () =>
            {
                if (animator != null)
                {
                    animator.SetBool("IsAttacking", false);
                }
            });
        }
        if (currentWeapon == null || !hasWeapon) return;
        if (currentWeapon.IsFlying) return;
        if (Time.time < nextAttackTime) return;

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
        attackRoutine = StartCoroutine(AttackRoutine());
        
       
    }

    private IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(attackDelay);
        if (currentWeapon != null && attackTarget != null)
        {
            ThrowWeapon();
        }
        yield return new WaitForSeconds(0.1f);
        if (animator != null)
        {
            animator.SetBool("IsAttacking", false);
        }
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
        if (scoreDisplay != null)
        {
            scoreDisplay.SetScore(value);
            UpdateCharacterStats();
        }
    }

    private void UpdateCharacterStats()
    {
        if (scoreDisplay == null) return;

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
        isDead = false;
        health.isDead = false;
        hasWeapon = true;
        scoreDisplay.gameObject.SetActive(true);
        this.gameObject.layer = LayerMask.NameToLayer("Enemy");
        characterCollider.enabled = true;
        if (currentWeapon != null)
        {
            currentWeapon.transform.SetParent(weaponSpawnPoint);
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
        if (currentWeapon != null)
        {
            currentWeapon.gameObject.SetActive(false);
        }

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
        if (health.isDead == true)
        {
            StopAllCoroutines();
            scoreDisplay.gameObject.SetActive(false);
            characterCollider.enabled = false;
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                agent.ResetPath();
            }
            isDead = true;
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}