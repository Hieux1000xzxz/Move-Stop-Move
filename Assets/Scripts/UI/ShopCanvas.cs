using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ShopCanvas : BaseCanvas
{
    [Header("UI References")]
    [SerializeField] private Transform weaponsGrid;
    [SerializeField] private GameObject weaponItemPrefab;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button selectButton;
    [SerializeField] private Button closeButton;

    [Header("Weapons Data")]
    [SerializeField] private WeaponData[] weapons;
    [SerializeField] private Player player;
    [SerializeField] private PlayerPreview previewPlayer;

    private WeaponData selectedWeapon;
    private Dictionary<string, WeaponItem> weaponItems = new Dictionary<string, WeaponItem>();

    private void Start()
    {
        buyButton.onClick.AddListener(OnBuyWeapon);
        selectButton.onClick.AddListener(OnSelectWeapon);
        closeButton.onClick.AddListener(CloseShop);
        InitializeWeaponsGrid();
        
    }

    private void InitializeWeaponsGrid()
    {
        foreach (Transform child in weaponsGrid)
        {
            Destroy(child.gameObject);
        }

        weaponItems.Clear();

        foreach (WeaponData weapon in weapons)
        {
            GameObject weaponItemObj = Instantiate(weaponItemPrefab, weaponsGrid);
            WeaponItem weaponItem = weaponItemObj.GetComponent<WeaponItem>();

            if (weaponItem != null)
            {
                bool isBought = PlayerPrefs.GetInt("WeaponBought_" + weapon.weaponName, 0) == 1;
                bool isSelected = PlayerPrefs.GetString("SelectedWeapon", "") == weapon.weaponName;
               
                weaponItem.Initialize(weapon, isBought, isSelected);
                weaponItem.OnWeaponSelected += OnWeaponSelected;

                weaponItems.Add(weapon.weaponName, weaponItem);
                buyButton.interactable = !isBought;
                selectButton.interactable = isBought && isSelected;
            }
        }
    }

    private void OnWeaponSelected(WeaponData weapon)
    {
        selectedWeapon = weapon;
        UpdateButtons();

        foreach (var item in weaponItems.Values)
        {
            item.SetChosen(item.WeaponData == weapon);
        }

        if (previewPlayer != null)
        {
            previewPlayer.ShowWeapon(weapon);
        }
    }

    private void UpdateButtons()
    {
        if (selectedWeapon == null) return;

        bool isBought = PlayerPrefs.GetInt("WeaponBought_" + selectedWeapon.weaponName, 0) == 1;
        bool isSelected = PlayerPrefs.GetString("SelectedWeapon", "") == selectedWeapon.weaponName;

        buyButton.interactable = !isBought;

        selectButton.interactable = isBought && !isSelected;
    }


    private void OnBuyWeapon()
    {
        if (selectedWeapon == null) return;

        // Kiểm tra đủ tiền (nếu cần)
        // int currentCoins = PlayerPrefs.GetInt("Coins", 0);
        // if (currentCoins >= selectedWeapon.price)
        // {
        //     currentCoins -= selectedWeapon.price;
        //     PlayerPrefs.SetInt("Coins", currentCoins);

        PlayerPrefs.SetInt("WeaponBought_" + selectedWeapon.weaponName, 1);
        PlayerPrefs.Save();

        // Cập nhật UI
        weaponItems[selectedWeapon.weaponName].SetBought(true);
        UpdateButtons();
        // }
        // else
        // {
        //     Debug.Log("Không đủ tiền!");
        // }
    }

    private void OnSelectWeapon()
    {
        if (selectedWeapon == null) return;

        PlayerPrefs.SetString("SelectedWeapon", selectedWeapon.weaponName);
        PlayerPrefs.Save();

        // Cập nhật tất cả items
        foreach (var item in weaponItems.Values)
        {
            bool isSelected = item.WeaponData.weaponName == selectedWeapon.weaponName;
            item.SetSelected(isSelected);
        }

        // Áp dụng vũ khí cho player
        if (player != null)
        {
            CharacterBase netChar = player.GetComponent<CharacterBase>();
            if (netChar != null && netChar.IsOwner)
            {
                netChar.RequestChangeWeaponServerRpc(selectedWeapon.weaponType);
            }
        }
        else
        {
            Debug.LogWarning("Player not found in scene!");
        }

        UpdateButtons();
    }

    public void LoadSelectedWeapon()
    {
        string selectedWeaponName = PlayerPrefs.GetString("SelectedWeapon", "");

        if (!string.IsNullOrEmpty(selectedWeaponName) && player != null)
        {
            foreach (WeaponData weapon in weapons)
            {
                if (weapon.weaponName == selectedWeaponName)
                {
                    player.ChangeWeapon(weapon.weaponType);
                    Debug.Log("Loaded selected weapon: " + weapon.weaponName);
                    return;
                }
            }
            Debug.LogWarning("Selected weapon not found: " + selectedWeaponName);
        }
        else if (player == null)
        {
            Debug.LogWarning("Player not found, cannot load selected weapon");
        }
    }

    private void CloseShop()
    {
        gameObject.SetActive(false);
        UIManager.Instance.OpenMainMenu();
    }

    private void OnDestroy()
    {
        // Hủy đăng ký event
        foreach (var item in weaponItems.Values)
        {
            if (item != null)
            {
                item.OnWeaponSelected -= OnWeaponSelected;
            }
        }
    }
}