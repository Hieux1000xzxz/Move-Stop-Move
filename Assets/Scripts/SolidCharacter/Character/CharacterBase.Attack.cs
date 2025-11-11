using UnityEngine;
using System.Collections;
using Unity.Netcode;


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
            if (isAttacking)
            {
                EndAttack(true);
                ChangeState(CharacterState.Move);
                return;
            }

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

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (resetState)
        {
            CharacterState newState = IsMovingNow() ? CharacterState.Move : CharacterState.Idle;
            currentState = newState;

            if (IsServer)
            {
                NetState.Value = newState;
                nextAttackTime = Time.time;
            }
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
        TriggerAttackAnimation();
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
        StartCoroutine(CancelAttack());
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

        bool isMultiplayer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        if (isMultiplayer && IsOwner)
        {
            if (attackTarget != null)
            {
                RequestLaunchServerRpc(attackTarget.position);
            }
        }
        else if (!isMultiplayer || IsServer)
        {
            LaunchWeaponLocal();
        }

        EndAttack();
    }

    private IEnumerator CancelAttack()
    {
        float timer = 0f;

        while (timer < attackDelay)
        {
            // 🚫 Nếu đang di chuyển, đổi state hoặc chết → hủy tấn công ngay lập tức
            if (IsMovingNow() || currentState != CharacterState.Attack || isDead)
            {
                EndAttack(true);
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }
    }

    private void LaunchWeaponLocal()
    {
        if (isDead || currentWeapon == null || attackTarget == null) return;
        if (currentState != CharacterState.Attack) return;

        Vector3 dir = (attackTarget.position - weaponSpawnPoint.position).normalized;
        Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(weaponRotationOffset);

        currentWeapon.transform.rotation = rot;
        currentWeapon.Launch(dir, this.gameObject);
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