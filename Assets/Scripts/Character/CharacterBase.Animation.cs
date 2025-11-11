using UnityEngine;

public partial class CharacterBase
{
    #region Animation Handling

    protected void SetAnimationSpeed(float speed)
    {
        if (IsOwner)
            netSpeed.Value = speed; // sync speed tới tất cả client
    }

    protected void SetAttackAnimation(bool isAttacking)
    {
        if (IsOwner)
            netIsAttacking.Value = isAttacking; // sync bool attackf
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

    #endregion
}