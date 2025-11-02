using UnityEngine;

public class PlayerPreview : MonoBehaviour
{
    [Header("Weapon Preview")]
    [SerializeField] private Transform weaponSpawnPoint; // default nếu có model sẵn
    [SerializeField] private Vector3 weaponRotationOffset;
    [SerializeField] private Vector3 weaponPositionOffset;

    [Header("Character Preview")]
    [SerializeField] private Transform previewRoot;

    [SerializeField] private WeaponData[] allWeapons;

    private GameObject currentWeaponObj;
    private GameObject currentPreview;

    //────────────────────────────────────
    public void ShowCharacter(CharacterData character)
    {
        ClearPreview();
        if (character == null) return;

        GameObject prefab = character.PreviewPrefab != null ? character.PreviewPrefab : character.Prefab;
        if (prefab == null) return;

        currentPreview = Instantiate(prefab, previewRoot);
        currentPreview.transform.localPosition = Vector3.zero;
        currentPreview.transform.localRotation = Quaternion.identity;
        currentPreview.transform.localScale = Vector3.one;

        if (character.HandSocket != null)
        {
            string socketName = character.HandSocket.name;

            Transform[] children = currentPreview.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in children)
            {
                if (t.name == socketName)
                {
                    weaponSpawnPoint = t;
                    break;
                }
            }
        }

        ShowCurrentWeapon();
    }

    //────────────────────────────────────
    public void ShowWeapon(WeaponData weaponData)
    {
        ClearWeapon();
        if (weaponData == null || weaponData.weaponPrefab == null || weaponSpawnPoint == null)
            return;

        currentWeaponObj = Instantiate(
            weaponData.weaponPrefab,
            weaponSpawnPoint.position + weaponPositionOffset,
            weaponSpawnPoint.rotation * Quaternion.Euler(weaponRotationOffset),
            weaponSpawnPoint
        );
    }

    //────────────────────────────────────
    private void ShowCurrentWeapon()
    {
        string selected = PlayerPrefs.GetString("SelectedWeapon", "");
        if (string.IsNullOrEmpty(selected)) return;

        for (int i = 0; i < allWeapons.Length; i++)
        {
            if (allWeapons[i].weaponName == selected)
            {
                ShowWeapon(allWeapons[i]);
                break;
            }
        }
    }

    private void ClearPreview()
    {
        for (int i = previewRoot.childCount - 1; i >= 0; i--)
            Destroy(previewRoot.GetChild(i).gameObject);
    }

    private void ClearWeapon()
    {
        if (currentWeaponObj != null)
        {
            Destroy(currentWeaponObj);
            currentWeaponObj = null;
        }
    }
}
