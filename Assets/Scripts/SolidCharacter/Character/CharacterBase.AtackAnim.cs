using UnityEngine;
using Unity.Netcode;
using System.Collections;

public partial class CharacterBase
{
    #region Attack Animation Event

    [ServerRpc]
    private void RequestLaunchServerRpc(ServerRpcParams rpcParams = default)
    {
        if (isDead || currentWeapon == null || attackTarget == null)
        {
            return;
        }

        if (currentState != CharacterState.Attack) return;
        Vector3 dir = (attackTarget.position - weaponSpawnPoint.position).normalized;
        Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(weaponRotationOffset);

        currentWeapon.transform.rotation = rot;
        currentWeapon.Launch(dir, this.gameObject);

        LaunchWeaponClientRpc(dir, rot);
    }

    [ClientRpc]
    private void LaunchWeaponClientRpc(Vector3 dir, Quaternion rot, ClientRpcParams rpcParams = default)
    {
        if (!isActiveAndEnabled) return;
        StartCoroutine(WaitUntilWeaponReady(dir, rot));
    }

    private IEnumerator WaitUntilWeaponReady(Vector3 dir, Quaternion rot)
    {
        currentWeapon.transform.rotation = rot;

        //shoot real weapon
        currentWeapon.Launch(dir, this.gameObject);
        yield return new WaitUntil(() => currentWeapon != null);
    }

    [ServerRpc]
    public void RequestChangeWeaponServerRpc(WeaponType newWeaponType)
    {
        ChangeWeapon(newWeaponType);
    }

    #endregion
}