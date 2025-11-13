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
        SyncStateToNetwork(newState);
        HandleAttackStateChange(newState);
    }

    private void SyncStateToNetwork(CharacterState newState)
    {
        if (IsServer)
        {
            NetState.Value = newState;
        }
    }

    private void HandleAttackStateChange(CharacterState newState)
    {
        if (IsServer && newState == CharacterState.Attack)
        {
            StopAgentMovement();
        }
    }

    private void StopAgentMovement()
    {
        if (!AgentValid) return;

        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
    }

    protected void HandleIdle()
    {
        Vector3 input = GetMovementInput();

        if (ShouldMove(input))
        {
            ChangeState(CharacterState.Move);
        }
        else
        {
            CheckForAttack();
        }
    }

    private bool ShouldMove(Vector3 input)
    {
        return input.magnitude > 0.01f;
    }

    protected void HandleMove()
    {
        Vector3 input = GetMovementInput();

        if (ShouldMove(input))
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
        if (!IsValidAttackTarget())
        {
            CancelAttackIfNotAttacking();
            return;
        }

        if (!IsTargetInRange())
        {
            CancelAttackIfNotAttacking();
            return;
        }

        ExecuteAttackBehavior();
    }

    private void CancelAttackIfNotAttacking()
    {
        if (!isAttacking)
        {
            EndAttack();
            ChangeState(CharacterState.Idle);
        }
    }

    private bool IsTargetInRange()
    {
        if (attackTarget == null) return false;

        float distance = Vector3.Distance(transform.position, attackTarget.position);
        return distance <= attackRange;
    }

    private void ExecuteAttackBehavior()
    {
        FaceTarget(attackTarget.position);

        if (!isAttacking)
        {
            PerformAttack();
        }

        StopAgentMovement();
    }

    private bool IsValidAttackTarget()
    {
        if (attackTarget == null) return false;

        if (!IsTargetAlive(attackTarget, out _))
        {
            ClearTargets();
            return false;
        }

        return true;
    }

    private void ClearTargets()
    {
        attackTarget = null;
        detectedTarget = null;
    }

    #endregion

    #region Attack Flow

    protected void FinishAttack(bool resetState = true)
    {
        ResetAttackFlags();
        StopAttackRoutine();

        if (resetState)
        {
            UpdateStateAfterAttack();
        }
    }

    private void ResetAttackFlags()
    {
        isAttacking = false;
        hasWeapon = true;

        if (IsOwner)
        {
            netIsAttacking.Value = false;
        }
    }

    private void StopAttackRoutine()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
    }

    private void UpdateStateAfterAttack()
    {
        CharacterState newState = IsMovingNow() ? CharacterState.Move : CharacterState.Idle;
        currentState = newState;

        if (IsServer)
        {
            NetState.Value = newState;
            nextAttackTime = Time.time;
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

        return IsPlayerMovingInput();
    }

    private bool IsPlayerMovingInput()
    {
        if (this is Player player)
        {
            return player.isMovingInput;
        }

        return false;
    }

    private void HandleCurrentAttackTarget()
    {
        if (ShouldSkipCurrentTarget()) return;

        if (!IsCurrentTargetValid())
        {
            CancelCurrentAttack();
        }
    }

    private bool ShouldSkipCurrentTarget()
    {
        return attackTarget == null || attackTarget == detectedTarget;
    }

    private bool IsCurrentTargetValid()
    {
        if (!IsTargetAlive(attackTarget, out _))
        {
            return false;
        }

        float distance = Vector3.Distance(transform.position, attackTarget.position);
        bool isActive = attackTarget.gameObject.activeInHierarchy;

        return distance <= attackRange && isActive;
    }

    private void CancelCurrentAttack()
    {
        attackTarget = null;
        EndAttack(true);
        ChangeState(CharacterState.Idle);
    }

    private void HandleNewDetectedTarget()
    {
        if (detectedTarget == null) return;

        if (!IsTargetAlive(detectedTarget, out CharacterBase targetChar))
        {
            detectedTarget = null;
            return;
        }

        TryStartAttackOnDetected();
    }

    private void TryStartAttackOnDetected()
    {
        float distance = Vector3.Distance(transform.position, detectedTarget.position);

        if (distance <= attackRange)
        {
            StartAttackOnTarget();
        }
    }

    private void StartAttackOnTarget()
    {
        bool shouldAttack = currentState != CharacterState.Attack ||
                            attackTarget != detectedTarget;

        if (shouldAttack)
        {
            attackTarget = detectedTarget;
            ChangeState(CharacterState.Attack);
        }
    }

    private bool IsTargetAlive(Transform target, out CharacterBase character)
    {
        character = null;

        if (!IsTargetActive(target)) return false;
        if (!target.TryGetComponent(out character)) return false;

        return !character.isDead && !character.IsHealthDead;
    }

    private bool IsTargetActive(Transform target)
    {
        return target != null && target.gameObject.activeInHierarchy;
    }

    #endregion

    #region Attack Execution

    protected void FaceTarget(Vector3 targetPosition)
    {
        Vector3 direction = GetDirectionToTarget(targetPosition);

        if (direction != Vector3.zero)
        {
            RotateTowardsTarget(direction);
        }
    }

    private Vector3 GetDirectionToTarget(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;
        return direction;
    }

    private void RotateTowardsTarget(Vector3 direction)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 30f);
    }

    protected void PerformAttack()
    {
        if (!CanStartAttack()) return;

        BeginAttack();
    }

    private bool CanStartAttack()
    {
        if (!IsValidAttackState()) return false;
        if (IsAgentMoving()) return false;
        if (!HasWeaponReady()) return false;

        return true;
    }

    private bool IsValidAttackState()
    {
        return currentState == CharacterState.Idle ||
               currentState == CharacterState.Attack;
    }

    private bool IsAgentMoving()
    {
        return AgentValid && agent.velocity.magnitude > 0.01f;
    }

    private bool HasWeaponReady()
    {
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
        SetAttackFlags();
        ScheduleNextAttack();
        StartAttackRoutine();
    }

    private void SetAttackFlags()
    {
        isAttacking = true;
        hasWeapon = false;

        if (IsOwner)
        {
            netIsAttacking.Value = true;
        }
    }

    private void ScheduleNextAttack()
    {
        nextAttackTime = Time.time + attackDelay;
    }

    private void StartAttackRoutine()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
        }

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

        if (ShouldCancelAttackRoutine())
        {
            EndAttack(true);
            yield break;
        }

        ExecuteWeaponLaunch();
        EndAttack();
    }

    private bool ShouldCancelAttackRoutine()
    {
        if (currentState != CharacterState.Attack) return true;
        if (isDead) return true;
        if (IsMovingNow()) return true;
        if (!IsWeaponReady()) return true;
        if (!IsValidAttackTarget()) return true;

        return false;
    }

    private bool IsWeaponReady()
    {
        return currentWeapon != null && !currentWeapon.IsFlying;
    }

    private void ExecuteWeaponLaunch()
    {
        bool isMultiplayer = NetworkManager.Singleton != null &&
                             NetworkManager.Singleton.IsListening;

        if (isMultiplayer && IsOwner)
        {
            LaunchWeaponMultiplayer();
        }
        else if (!isMultiplayer || IsServer)
        {
            LaunchWeaponLocal();
        }
    }

    private void LaunchWeaponMultiplayer()
    {
        if (attackTarget != null && IsTargetAlive(attackTarget, out _))
        {
            RequestLaunchServerRpc(attackTarget.position);
        }
    }

    private bool LaunchWeaponLocal()
    {
        if (!CanLaunchWeapon()) return false;

        Vector3 dir = CalculateLaunchDirection();
        Quaternion rot = CalculateLaunchRotation(dir);

        currentWeapon.transform.rotation = rot;
        currentWeapon.Launch(dir, this.gameObject);

        return true;
    }

    private bool CanLaunchWeapon()
    {
        if (isDead) return false;
        if (currentWeapon == null) return false;
        if (attackTarget == null) return false;
        if (currentState != CharacterState.Attack) return false;
        if (!IsTargetAlive(attackTarget, out _)) return false;

        return true;
    }

    private Vector3 CalculateLaunchDirection()
    {
        return (attackTarget.position - weaponSpawnPoint.position).normalized;
    }

    private Quaternion CalculateLaunchRotation(Vector3 direction)
    {
        return Quaternion.LookRotation(direction) * Quaternion.Euler(weaponRotationOffset);
    }

    public void OnWeaponReturned()
    {
        FinishAttack();
    }

    protected void EndAttack(bool cancelByMove = false)
    {
        FinishAttack();

        if (cancelByMove)
        {
            ClearTargets();
        }
    }

    #endregion
}