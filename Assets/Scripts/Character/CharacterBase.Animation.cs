using UnityEngine;
using Unity.Netcode;

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

    protected void SetSuperSpeedAnimation(bool isActive)
    {
        if (animator != null)
        {
            animator.SetBool("SuperSpeed", isActive);
        }

        if (IsMultiplayer && IsOwner)
        {
            SetSuperSpeedServerRpc(isActive);
        }
    }

    [ServerRpc]
    private void SetSuperSpeedServerRpc(bool isActive)
    {
        SetSuperSpeedClientRpc(isActive);
    }

    [ClientRpc]
    private void SetSuperSpeedClientRpc(bool isActive)
    {
        if (!IsOwner && animator != null)
        {
            animator.SetBool("SuperSpeed", isActive);
        }
    }

    #endregion
}