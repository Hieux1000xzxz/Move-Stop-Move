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

    [Header("Weapon Settings")]
    [SerializeField] protected Transform weaponSpawnPoint;
    [SerializeField] protected Vector3 weaponRotationOffset = Vector3.zero;
    [SerializeField] protected WeaponBase weaponPrefab;
    [SerializeField] protected float attackDelay = 0.3f;
    protected WeaponBase currentWeapon;

    [Header("Score Settings")]
    [SerializeField] private KillScoreDisplay scoreDisplay;
    [SerializeField] private float sizePerScore = 0.05f;
    [SerializeField] private float rangePerScore = 0.1f;
    [SerializeField] private float moveSpeedPerScore = 0.1f;
    [SerializeField] private float maxScale = 3f;

    protected CharacterState currentState = CharacterState.Idle;
    protected Transform attackTarget;
    protected bool isAttacking = false;
    protected bool isMoving = false;
    private Coroutine attackRoutine;
    protected bool hasWeapon = true;
    public float currentAttackRange => attackRange;
    protected virtual void Awake()
    {
        InitializeAgent();
        InitializeWeapon();
    }

    private void InitializeAgent()
    {
        if (agent != null)
        {
            agent.speed = moveSpeed;
        }
        else
        {
            Debug.LogError($"NavMeshAgent is missing on {gameObject.name}");
        }
    }

    private void InitializeWeapon()
    {
        if (weaponPrefab != null && weaponSpawnPoint != null)
        {
            currentWeapon = Instantiate(weaponPrefab, weaponSpawnPoint);
            currentWeapon.Init(this, weaponSpawnPoint);
        }
        else
        {
            Debug.LogWarning($"Weapon prefab or spawn point is missing on {gameObject.name}");
        }
    }

    protected virtual void Update()
    {
        if (agent == null || !agent.isActiveAndEnabled) return;

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

        // Tăng kích thước
        float newScale = Mathf.Min(1f + scoreDisplay.CurrentScore * sizePerScore, maxScale);
        transform.localScale = Vector3.one * newScale;
        currentWeapon.transform.localScale = Vector3.one * newScale;
        // Tăng tầm tấn công
        attackRange += scoreDisplay.CurrentScore * rangePerScore;

        // Tăng tốc độ di chuyển
        moveSpeed += moveSpeedPerScore;
        if (agent != null && agent.isActiveAndEnabled)
        {
            agent.speed = moveSpeed;
        }
    }

    protected void ChangeState(CharacterState newState)
    {
        if (currentState != newState)
        {
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

        if (!isAttacking)
        {
            PerformAttack();
            FaceTarget(attackTarget.position);
        }
    }

    protected virtual void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }
    }

    protected virtual void PerformAttack()
    {
        if (currentWeapon == null || !hasWeapon) return;
        if (currentWeapon.IsFlying) return;

        isAttacking = true;
        hasWeapon = false;

        if (animator != null)
            animator.SetTrigger("Attack");

        // chạy coroutine delay trước khi ném
        StartCoroutine(ThrowAfterDelay());
    }

    private IEnumerator ThrowAfterDelay()
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

        if (currentState == CharacterState.Attack)
        {
            Vector3 input = GetMovementInput();
            if (input.magnitude > 0.1f)
            {
                ChangeState(CharacterState.Move);
            }
            else
            {
                ChangeState(CharacterState.Idle);
            }
        }
    }

    protected virtual void CheckForAttack()
    {
        // Implement ở lớp con (Player, Enemy)
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
    }

    public abstract Vector3 GetMovementInput();

    protected virtual void OnDestroy()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
        }
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}