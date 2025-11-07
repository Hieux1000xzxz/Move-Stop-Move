using UnityEngine;
using Unity.Netcode;
using UnityEngine.AI;
public partial class CharacterBase
{
    protected bool IsMovingNow()
    {
        if (this is Player player)
        {
            if (player.IsOwner)
                return player.isMovingInput;
            if (player.NetIsMoving.Value)
                return true;

            if (player.NetSpeed.Value > 0.1f)
                return true;

            if (AgentValid)
                return agent.velocity.magnitude > 0.05f;

            return false;
        }

        // AI or non-player
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
            agent.Move(direction.normalized * moveSpeed * Time.deltaTime);
        }
        else
        {
            agent.ResetPath();
        }
    }
    
    protected virtual void UpdateAnimator()
    {
        if (animator == null) return;

        float speed = 0f;
        if (AgentValid)
        {
            speed = agent.velocity.magnitude;
        }
        else
        {
            Vector3 delta = (transform.position - lastPosition);
            delta.y = 0f;
            speed = (Time.deltaTime > 0f) ? (delta.magnitude / Time.deltaTime) : 0f;
        }

        animator.SetFloat("Speed", speed);

        bool attackingNow = IsOwner ? isAttacking : NetIsAttacking.Value;
        animator.SetBool("IsAttacking", attackingNow);

        lastPosition = transform.position;
    }
    
    public abstract Vector3 GetMovementInput();
}
