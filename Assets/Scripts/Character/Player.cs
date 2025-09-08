using System.Globalization;
using UnityEngine;

public class Player : CharacterBase
{
    [SerializeField] private FloatingJoystick joystick;
    public FloatingJoystick Joystick => joystick;
    private bool isMovingInput;

    protected override void Update()
    {
        base.Update();
        Vector3 input = GetMovementInput();
        isMovingInput = input.magnitude > 0.01f;

        if (isMovingInput && currentState == CharacterState.Attack)
        {
            EndAttack();
            ChangeState(CharacterState.Move);
        }
    }

    protected override void UpdateAnimator()
    {
        if (animator == null) return;

        bool isMovingNow = isMovingInput && !isAttacking;
        animator.SetBool("IsMoving", isMovingNow);
    }

    protected override void OnTargetLost(Transform lostTarget)
    {
        base.OnTargetLost(lostTarget);
        if (isMovingInput && currentState == CharacterState.Attack)
        {
            ChangeState(CharacterState.Move);
        }
    }

    protected override void OnNewTargetFound(Transform newTarget)
    {
        base.OnNewTargetFound(newTarget);
        if (!isMovingInput)
        {
            float distance = Vector3.Distance(transform.position, newTarget.position);
            if (distance <= attackRange)
            {
                attackTarget = newTarget;
                ChangeState(CharacterState.Attack);
            }
        }
    }

    public override Vector3 GetMovementInput()
    {
        return new Vector3(joystick.Horizontal, 0f, joystick.Vertical);
    }
}