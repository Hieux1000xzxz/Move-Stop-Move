using UnityEngine;
using Unity.Netcode;

public partial class CharacterBase
{
    #region Animation Handling

    // ✅ Speed (Float) - Cần RPC để sync
    protected void SetAnimationSpeed(float speed)
    {
        if (networkAnimator == null || networkAnimator.Animator == null) return;

        // Set local ngay (responsive)
        networkAnimator.Animator.SetFloat("Speed", speed);

        // Owner gửi lên Server để sync
        if (IsOwner && !IsServer)
        {
            RequestSetSpeedServerRpc(speed);
        }
        else if (IsServer)
        {
            SyncSpeedClientRpc(speed);
        }
    }

    protected void TriggerAttackAnimation()
    {
        if (networkAnimator == null) return;

        networkAnimator.SetTrigger("IsAttacking");
    }

    protected void TriggerDeathAnimation()
    {
        if (networkAnimator == null || networkAnimator.Animator == null) return;

        networkAnimator.Animator.SetFloat("Speed", 0f);
        networkAnimator.SetTrigger("Death");
    }

    #endregion

    #region Network RPCs

    [ServerRpc]
    private void RequestSetSpeedServerRpc(float speed)
    {
        if (networkAnimator == null || networkAnimator.Animator == null) return;

        networkAnimator.Animator.SetFloat("Speed", speed);
        SyncSpeedClientRpc(speed);
    }

    [ClientRpc]
    private void SyncSpeedClientRpc(float speed)
    {
        // Chỉ set cho clients khác (Owner đã set local rồi)
        if (!IsOwner)
        {
            if (networkAnimator != null && networkAnimator.Animator != null)
                networkAnimator.Animator.SetFloat("Speed", speed);
        }
    }

    #endregion
}