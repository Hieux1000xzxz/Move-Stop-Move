using UnityEngine;
using System.Linq;
using System.Collections;

public class LobbyPreviewManager : MonoBehaviour
{
    [Header("Preview Setup")]
    [SerializeField] private PlayerPreview playerPreview;
    
    [Header("Data Arrays")]
    [SerializeField] private WeaponData[] allWeapons;
    [SerializeField] private CharacterData[] allCharacters;

    private void Start()
    {
        StartCoroutine(ShowPreviewDelayed());
    }

    private IEnumerator ShowPreviewDelayed()
    {
        yield return null;
        yield return null;
        
        ShowEquippedPreview();
    }

    private void ShowEquippedPreview()
    {
        if (playerPreview == null)
        {
            return;
        }

        if (allCharacters == null || allCharacters.Length == 0)
        {
            return;
        }

        if (allWeapons == null || allWeapons.Length == 0)
        {
            return;
        }

        string equippedCharName = PlayerPrefs.GetString("SelectedCharacter", "");
        CharacterData equippedChar = FindCharacterByName(equippedCharName);
        
        string equippedWeapName = PlayerPrefs.GetString("SelectedWeapon", "");
        WeaponData equippedWeapon = FindWeaponByName(equippedWeapName);
        
        if (equippedChar != null && equippedWeapon != null)
        {
            playerPreview.ShowCharacterWithWeapon(equippedChar, equippedWeapon);
        }
       
    }

    private CharacterData FindCharacterByName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return allCharacters[0];
        }
        
        CharacterData found = allCharacters.FirstOrDefault(c => c.Name == name);
        
        if (found == null)
        {
            return allCharacters[0];
        }
        
        return found;
    }

    private WeaponData FindWeaponByName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return allWeapons[0];
        }
        
        WeaponData found = allWeapons.FirstOrDefault(w => w.weaponName == name);
        
        if (found == null)
        {
            return allWeapons[0];
        }
        
        return found;
    }
}