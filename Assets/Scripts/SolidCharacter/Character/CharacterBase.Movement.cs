using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI;

public partial class CharacterBase
{
    protected virtual bool IsMovingNow()
    {
        if (this is Player player)
        {
            return player.isMovingInput;
        }

        if (AgentValid)
            return agent.velocity.magnitude > 0.05f;

        return false;
    }

    protected virtual void Move(Vector3 direction)
    {
        if (agent == null || !agent.isActiveAndEnabled) return;
        if (isDead) return;

        if (direction.magnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
            agent.Move(direction * (moveSpeed * Time.deltaTime));
        }
        else
        {
            agent.ResetPath();
        }
    }

    protected virtual void UpdateAnimator()
    {
        if (networkAnimator == null || networkAnimator.Animator == null) return;

        float speed = CalculateAnimationSpeed();

        SetAnimationSpeed(speed);
        //SetAttackAnimation(isAttacking);
    }

    private float CalculateAnimationSpeed()
    {
        if (AgentValid)
            return agent.velocity.magnitude;

        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        float speed = (Time.deltaTime > 0f) ? (delta.magnitude / Time.deltaTime) : 0f;
        lastPosition = transform.position;

        return speed;
    }

    public abstract Vector3 GetMovementInput();
}