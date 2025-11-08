using UnityEngine;
using System.Collections;

public partial class CharacterBase
{
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
            if (AgentValid)
            {
                agent.isStopped = true;
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
        }
    }

    protected void HandleIdle()
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

        if (AgentValid)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }

    #endregion

    #region Attack

    protected void FinishAttack(bool resetState = true)
    {
        isAttacking = false;
        hasWeapon = true;

        if (IsOwner)
            SetAttackAnimation(false);

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (IsServer)
        {
            nextAttackTime = Time.time;
            if (resetState)
                ChangeState(IsMovingNow() ? CharacterState.Move : CharacterState.Idle);
        }
    }

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
            if (player.isMovingInput)
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

        if (AgentValid && agent.velocity.magnitude > 0.01f)
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
        hasWeapon = false;
        nextAttackTime = Time.time + attackDelay;

        if (IsOwner)
            SetAttackAnimation(true);

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
            RequestLaunchServerRpc();

        EndAttack();
    }

    public void OnWeaponReturned()
    {
        FinishAttack();
    }

    protected void EndAttack(bool cancelByMove = false)
    {
        FinishAttack();
    }

    #endregion
}