using UnityEngine;
using Unity.Netcode;
using System.Collections;

public partial class CharacterBase
{
    [ServerRpc]
    public void RequestPickupPowerupServerRpc(NetworkObjectReference powerupRef, PowerupType type, float duration)
    {
        if (!powerupRef.TryGet(out NetworkObject powerupObj)) return;

        ApplyPowerupClientRpc(type, duration);

        powerupObj.Despawn();
    }

    [ClientRpc]
    public void ApplyPowerupClientRpc(PowerupType type, float duration)
    {
        if (IsOwner) return;

        switch (type)
        {
            case PowerupType.SpeedBoost:
                StartCoroutine(ApplySpeedBoostLocal(duration));
                break;

            case PowerupType.WeaponGrow:
                StartCoroutine(ApplyWeaponGrowLocal(duration));
                break;
        }
    }

    private IEnumerator ApplySpeedBoostLocal(float duration, float multiplier = 2f)
    {
        if (isSpeedBoostActive)
        {
            StopCoroutine(speedBoostRoutine);
        }
        else
        {
            isSpeedBoostActive = true;
            moveSpeed = baseMoveSpeed * multiplier;
            if (agent != null) agent.speed = moveSpeed;
            SetSuperSpeedAnimation(true);
        }

        speedBoostRoutine = StartCoroutine(SpeedBoostTimer(duration));
        yield break;
    }

    private IEnumerator SpeedBoostTimer(float duration)
    {
        yield return new WaitForSeconds(duration);

        moveSpeed = baseMoveSpeed;
        if (agent != null) agent.speed = moveSpeed;

        SetSuperSpeedAnimation(false);

        isSpeedBoostActive = false;
        speedBoostRoutine = null;
    }

    private IEnumerator ApplyWeaponGrowLocal(float duration, float scaleMultiplier = 1.5f, float speedMultiplier = 1.3f)
    {
        if (currentWeaponPublic == null) yield break;

        if (isWeaponGrowActive)
        {
            StopCoroutine(weaponGrowRoutine);
        }
        else
        {
            ApplyWeaponState(currentWeaponPublic, scaleMultiplier, speedMultiplier);
        }

        weaponGrowRoutine = StartCoroutine(WeaponGrowTimer(duration));
    }

    private void ApplyWeaponState(WeaponBase weapon, float scaleMultiplier = 1f, float speedMultiplier = 1f)
    {
        if (weapon == null) return;

        isWeaponGrowActive = (scaleMultiplier != 1f || speedMultiplier != 1f);

        weapon.BuffScaleMultiplier = scaleMultiplier;
        weapon.Speed = weapon.OriginalSpeed * speedMultiplier;

        weapon.ApplyScale();
    }

    private IEnumerator WeaponGrowTimer(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (currentWeaponPublic != null)
        {
            ApplyWeaponState(currentWeaponPublic);
        }

        isWeaponGrowActive = false;
        weaponGrowRoutine = null;
    }

    public void ApplyPowerupLocal(PowerupType type, float duration)
    {
        switch (type)
        {
            case PowerupType.SpeedBoost:
                StartCoroutine(ApplySpeedBoostLocal(duration));
                break;

            case PowerupType.WeaponGrow:
                StartCoroutine(ApplyWeaponGrowLocal(duration));
                break;
        }
    }
}