using UnityEngine;

public class PlayerPreview : MonoBehaviour
{
    [Header("Weapon Preview")]
    [SerializeField] private Transform weaponSpawnPoint;
    [SerializeField] private Vector3 weaponRotationOffset;
    [SerializeField] private Vector3 weaponPositionOffset;

    [Header("Character Preview")]
    [SerializeField] private Transform previewRoot;

    private GameObject currentWeaponObj;
    private GameObject currentPreview;

    //────────────────────────────────────
    // ✅ MAIN ENTRY POINTS
    //────────────────────────────────────
    public void ShowCharacter(CharacterData character)
    {
        ClearPreview();
        GameObject prefab = GetCharacterPreviewPrefab(character);
        CreatePreviewInstance(prefab);
        FindWeaponSocket(character);
    }

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

    /// <summary>
    /// ✅ Combined version for shop: shows character + weapon together.
    /// </summary>
    public void ShowCharacterWithWeapon(CharacterData character, WeaponData weapon)
    {
        // Clear old previews
        ClearPreview();
        ClearWeapon();

        // Build character preview
        ShowCharacter(character);

        // Then attach weapon if available
        if (weapon != null)
        {
            // Delay one frame to ensure socket exists
            StartCoroutine(ShowWeaponNextFrame(weapon));
        }
    }

    private System.Collections.IEnumerator ShowWeaponNextFrame(WeaponData weapon)
    {
        yield return null; // wait one frame
        ShowWeapon(weapon);
    }

    //────────────────────────────────────
    // INTERNAL HELPERS
    //────────────────────────────────────
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
        if (character.HandSocket == null || currentPreview == null)
        {
            Debug.Log("CharacterHanSocket and CurrentPriview null");
            return;
        }

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

    private void ClearPreview()
    {
        for (int i = previewRoot.childCount - 1; i >= 0; i--)
            Destroy(previewRoot.GetChild(i).gameObject);

        currentPreview = null;
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
