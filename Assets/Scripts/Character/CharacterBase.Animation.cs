using UnityEngine;

public partial class CharacterBase
{
    #region Animation Handling

    protected void SetAnimationSpeed(float speed)
    {
        if (IsOwner)
            netSpeed.Value = speed;
    }

    protected void SetAttackAnimation(bool isAttacking)
    {
        if (IsOwner)
            netIsAttacking.Value = isAttacking;
    }

    protected void TriggerDeathAnimation()
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsAttacking", false);
            if (networkAnimator != null)
                networkAnimator.SetTrigger("Death");
        }
    }

    protected void SetAnimatorParameters(float speed, bool attacking)
    {
        if (animator == null) return;
        animator.SetFloat("Speed", speed);
        animator.SetBool("IsAttacking", attacking);
    }

    protected void SyncAnimationToNetwork(float speed, bool attacking)
    {
        if (!IsMultiplayer) return;

        if (IsOwner || IsServer)
        {
            netSpeed.Value = speed;
            netIsAttacking.Value = attacking;
        }
    }

    #endregion
}