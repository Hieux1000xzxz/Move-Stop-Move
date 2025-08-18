using UnityEngine;

public class Player : CharacterBase
{
    [SerializeField] private FloatingJoystick joystick;
    [SerializeField] private LayerMask enemyLayer;

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

    protected override void CheckForAttack()
    {
        if (!isMovingInput && currentState != CharacterState.Attack && !isAttacking)
        {
            Collider[] enemies = Physics.OverlapSphere(transform.position, attackRange, enemyLayer);
            if (enemies.Length > 0)
            {
                attackTarget = GetNearestEnemy(enemies);
                ChangeState(CharacterState.Attack);
            }
        }
    }

    private Transform GetNearestEnemy(Collider[] enemies)
    {
        Transform nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (var enemy in enemies)
        {
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = enemy.transform;
            }
        }

        return nearest;
    }

    public override Vector3 GetMovementInput()
    {
        return new Vector3(joystick.Horizontal, 0f, joystick.Vertical);
    }
}
