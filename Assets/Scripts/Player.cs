using UnityEngine;

public class Player : CharacterBase
{
    [SerializeField] private FloatingJoystick joystick;
    private bool isMovingInput;

    protected override void Update()
    {
        Vector3 input = GetMovementInput();
        isMovingInput = input.magnitude > 0.05f;

        if (isMovingInput && currentState == CharacterState.Attack)
        {
            EndAttack();
            ChangeState(CharacterState.Move);
        }

        base.Update();
    }

    protected override void OnTargetLost(Transform lostTarget)
    {
        base.OnTargetLost(lostTarget);

        // Player có thể tiếp tục di chuyển sau khi mất target
        if (isMovingInput && currentState == CharacterState.Attack)
        {
            ChangeState(CharacterState.Move);
        }
    }

    protected override void OnNewTargetFound(Transform newTarget)
    {
        base.OnNewTargetFound(newTarget);

        // Player chỉ tấn công khi không di chuyển
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