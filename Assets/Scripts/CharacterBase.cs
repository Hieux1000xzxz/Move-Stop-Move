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
    [SerializeField] private string weaponTag;
    [SerializeField] protected Transform weaponSpawnPoint;
    [SerializeField] protected Vector3 weaponRotationOffset = Vector3.zero;

    protected CharacterState currentState = CharacterState.Idle;
    protected Transform attackTarget;
    protected bool isAttacking = false;
    protected bool isMoving = false;
    protected float attackDelay;
    private Coroutine attackRoutine;

    protected virtual void Awake()
    {
        agent.speed = moveSpeed;

    }

    protected virtual void Update()
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
        UpdateAnimator();
    }

    protected void ChangeState(CharacterState newState)
    {
        currentState = newState;
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
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    protected virtual void PerformAttack()
    {
        isAttacking = true;
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        attackRoutine = StartCoroutine(AttackRoutine());

        ThrowWeapon();
    }

    protected virtual void ThrowWeapon()
    {
        if (weaponSpawnPoint != null && attackTarget != null)
        {
            Vector3 dir = (attackTarget.position - weaponSpawnPoint.position).normalized;
            Quaternion customRot = Quaternion.LookRotation(dir) * Quaternion.Euler(weaponRotationOffset);
            WeaponManager.Instance.SpawnProjectile(
                weaponTag,
                weaponSpawnPoint.position,
                dir,
                customRot,
                this.gameObject);
        }
    }

    private IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(attackDuration);
        EndAttack();
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
        // Implement ở lớp con
    }

    protected virtual void Move(Vector3 direction)
    {
        if (isAttacking) return;

        Vector3 targetPos = transform.position + direction;
        agent.SetDestination(targetPos);
    }

    protected virtual void UpdateAnimator()
    {
        isMoving = agent.velocity.magnitude > 0.1f && !isAttacking;
        animator.SetBool("IsMoving", isMoving);
        animator.SetBool("IsAttacking", isAttacking);
    }

    public abstract Vector3 GetMovementInput();
}
