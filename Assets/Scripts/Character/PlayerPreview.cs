using UnityEngine;

public class PlayerPreview : MonoBehaviour
{
    [Header("Weapon Preview")]
    [SerializeField] private Transform weaponSpawnPoint; 
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

        GameObject prefab = GetCharacterPreviewPrefab(character);
        if (prefab == null) return;

        CreatePreviewInstance(prefab);
        FindWeaponSocket(character);
    }

    private GameObject GetCharacterPreviewPrefab(CharacterData character)
    {
        return character.PreviewPrefab != null ? character.PreviewPrefab : character.Prefab;
    }

    private void CreatePreviewInstance(GameObject prefab)
    {
        currentPreview = Instantiate(prefab, previewRoot);
        currentPreview.transform.localPosition = Vector3.zero;
        currentPreview.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); 
        currentPreview.transform.localScale = Vector3.one;
    }

    private void FindWeaponSocket(CharacterData character)
    {
        if (character.HandSocket == null || currentPreview == null) return;

        string socketName = character.HandSocket.name;
        Transform[] children = currentPreview.GetComponentsInChildren<Transform>(true);

        foreach (Transform t in children)
        {
            if (t.name == socketName)
            {
                weaponSpawnPoint = t;
                return;
            }
        }
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
