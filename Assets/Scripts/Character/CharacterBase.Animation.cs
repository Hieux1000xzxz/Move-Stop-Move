using UnityEngine;

public partial class CharacterBase
{
    #region Animation Handling

    protected void SetAnimationSpeed(float speed)
    {
        if (animator != null)
            animator.SetFloat("Speed", speed);

        if (networkAnimator != null)
            networkAnimator.Animator.SetFloat("Speed", speed);
    }

    protected void SetAttackAnimation(bool isAttacking)
    {
        if (animator != null)
            animator.SetBool("IsAttacking", isAttacking);

        if (networkAnimator != null)
            networkAnimator.Animator.SetBool("IsAttacking", isAttacking);
    }

    protected void TriggerDeathAnimation()
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", 0f);
            animator.SetBool("IsAttacking", false);
            animator.SetTrigger("Death");
        }

        if (networkAnimator != null)
        {
            networkAnimator.Animator.SetFloat("Speed", 0f);
            networkAnimator.Animator.SetBool("IsAttacking", false);
            networkAnimator.SetTrigger("Death");
        }
    }

    #endregion
}