using UnityEngine;
using Unity.Netcode;
using System.Collections;

public partial class CharacterBase
{
    #region Attack Animation Event

    [ServerRpc]
    private void RequestLaunchServerRpc(Vector3 targetPos, ServerRpcParams rpcParams = default)
    {
        if (currentWeapon == null || weaponSpawnPoint == null) return;

        Vector3 dir = (targetPos - weaponSpawnPoint.position).normalized;
        Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(weaponRotationOffset);

        currentWeapon.transform.rotation = rot;
        currentWeapon.Launch(dir, this.gameObject);

        LaunchWeaponClientRpc(dir, rot);
    }

    [ClientRpc]
    private void LaunchWeaponClientRpc(Vector3 dir, Quaternion rot, ClientRpcParams rpcParams = default)
    {
        if (!isActiveAndEnabled) return;

        if (!IsServer)
        {
            if (currentWeapon != null && !currentWeapon.IsFlying)
            {
                currentWeapon.transform.rotation = rot;
                currentWeapon.Launch(dir, this.gameObject);
            }
        }
    }

    [ServerRpc]
    public void RequestChangeWeaponServerRpc(WeaponType newWeaponType)
    {
        ChangeWeapon(newWeaponType);
    }

    #endregion
}