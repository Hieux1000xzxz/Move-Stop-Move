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

        if (isAttacking && !hasWeapon && currentWeapon != null && !currentWeapon.IsFlying)
            return;
        
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

        // Blend Idle ↔ Move
        if (AgentValid)
            animator.SetFloat("Speed", agent.velocity.magnitude);
        animator.SetBool("IsAttacking", isAttacking);

        lastPosition = transform.position;
    }
    
    public abstract Vector3 GetMovementInput();
}
