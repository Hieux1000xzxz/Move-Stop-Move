using UnityEngine;

public class PlayerPreview : MonoBehaviour
{
    [SerializeField] private Transform weaponSpawnPoint;
    [SerializeField] private Vector3 weaponRotationOffset;
    [SerializeField] private Vector3 weaponPositionOffset;

    private GameObject currentWeaponObj;

    public void ShowWeapon(WeaponData weaponData)
    {
        if (currentWeaponObj != null)
        {
            Destroy(currentWeaponObj);
            currentWeaponObj = null;
        }

        if (weaponData.weaponPrefab != null)
        {
            currentWeaponObj = Instantiate(
                weaponData.weaponPrefab,
                weaponSpawnPoint.position + weaponPositionOffset,
                weaponSpawnPoint.rotation * Quaternion.Euler(weaponRotationOffset),
                weaponSpawnPoint
            );
        }
    }
}
