using UnityEngine;

public partial class CharacterBase
{
    #region Animation Handling

    protected void SetAnimationSpeed(float speed)
    {
        if (networkAnimator != null && networkAnimator.Animator != null)
        {
            networkAnimator.Animator.SetFloat("Speed", speed);
        }
    }

    protected void SetAttackAnimation(bool isAttacking)
    {
        if (networkAnimator != null && networkAnimator.Animator != null)
        {
            networkAnimator.Animator.SetBool("IsAttacking", isAttacking);
        }
    }

    protected void TriggerDeathAnimation()
    {
        if (networkAnimator != null && networkAnimator.Animator != null)
        {
            networkAnimator.Animator.SetFloat("Speed", 0f);
            networkAnimator.Animator.SetBool("IsAttacking", false);
            networkAnimator.SetTrigger("Death");
        }
    }

    #endregion
}