using UnityEngine;

public class PlayerPreview : MonoBehaviour
{
    [SerializeField] private Transform weaponSpawnPoint;
    [SerializeField] private Vector3 weaponRotationOffset; // góc xoay tùy chỉnh
    [SerializeField] private Vector3 weaponPositionOffset; // nếu bạn muốn dịch vị trí thêm

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
                weaponSpawnPoint.position + weaponPositionOffset,
                weaponSpawnPoint.rotation * Quaternion.Euler(weaponRotationOffset),
                weaponSpawnPoint
            );
        }
    }
}
