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
        if (!IsOwner && !IsHost) return;

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
        }

        speedBoostRoutine = StartCoroutine(SpeedBoostTimer(duration));
        yield break;
    }

    private IEnumerator SpeedBoostTimer(float duration)
    {
        yield return new WaitForSeconds(duration);
        
        moveSpeed = baseMoveSpeed;
        if (agent != null) agent.speed = moveSpeed;

        isSpeedBoostActive = false;
        speedBoostRoutine = null;
    }
    
    private IEnumerator ApplyWeaponGrowLocal(float duration, float scaleMultiplier = 1.5f, float speedMultiplier = 1.5f)
    {
        if (currentWeaponPublic == null) yield break;
        WeaponBase weapon = currentWeaponPublic;

        if (isWeaponGrowActive)
        {
            StopCoroutine(weaponGrowRoutine);
        }
        else
        {
            isWeaponGrowActive = true;

            weapon.BuffScaleMultiplier = scaleMultiplier;
            weapon.Speed = weapon.OriginalSpeed * speedMultiplier;
            
            weapon.ApplyScale();
        }

        weaponGrowRoutine = StartCoroutine(WeaponGrowTimer(duration));
    }


    private IEnumerator WeaponGrowTimer(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (currentWeaponPublic != null)
        {
            WeaponBase weapon = currentWeaponPublic;
            weapon.BuffScaleMultiplier = 1f;
            weapon.Speed = weapon.OriginalSpeed;
            
            weapon.ApplyScale();
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
