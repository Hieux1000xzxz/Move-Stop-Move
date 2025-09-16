using UnityEngine;

public class PlayerPreview : MonoBehaviour
{
    [SerializeField] private Transform weaponSpawnPoint;
    private GameObject currentWeaponObj;

    public void ShowWeapon(WeaponData weaponData)
    {
        // Xóa vũ khí cũ
        if (currentWeaponObj != null)
        {
            Destroy(currentWeaponObj);
            currentWeaponObj = null;
        }

        // Spawn vũ khí mới từ prefab
        if (weaponData.weaponPrefab != null)
        {
            currentWeaponObj = Instantiate(
                weaponData.weaponPrefab,
                weaponSpawnPoint.position,
                weaponSpawnPoint.rotation,
                weaponSpawnPoint
            );
        }
    }
}
