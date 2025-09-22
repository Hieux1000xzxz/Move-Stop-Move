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
        InitSelectedWeapon();
    }
    public void InitSelectedWeapon()
    {
        string selectedWeaponName = PlayerPrefs.GetString("SelectedWeapon", "");

        if (!string.IsNullOrEmpty(selectedWeaponName) && weaponItems.ContainsKey(selectedWeaponName))
        {
            // Đã có vũ khí được chọn từ trước
            WeaponItem selectedItem = weaponItems[selectedWeaponName];
            selectedWeapon = selectedItem.WeaponData;

            foreach (var item in weaponItems.Values)
            {
                bool isSelected = item == selectedItem;
                item.SetChosen(isSelected);
                item.SetSelected(isSelected);
            }

            if (previewPlayer != null)
            {
                previewPlayer.ShowWeapon(selectedWeapon);
            }

            UpdateButtons();
        }
        else
        {
            // 🔥 Lần đầu vào game -> mặc định lấy vũ khí đầu tiên trong danh sách
            if (weapons.Length > 0)
            {
                WeaponData defaultWeapon = weapons[0]; // vũ khí đầu tiên
                selectedWeapon = defaultWeapon;

                // Cập nhật PlayerPrefs để lưu lại
                PlayerPrefs.SetString("SelectedWeapon", defaultWeapon.weaponName);
                PlayerPrefs.SetInt("WeaponBought_" + defaultWeapon.weaponName, 1); // coi như đã mua
                PlayerPrefs.Save();

                // Cập nhật UI
                foreach (var item in weaponItems.Values)
                {
                    bool isSelected = item.WeaponData.weaponName == defaultWeapon.weaponName;
                    item.SetBought(isSelected);   // vũ khí đầu tiên đã mua
                    item.SetSelected(isSelected); // vũ khí đầu tiên được chọn
                    item.SetChosen(isSelected);
                }

                if (previewPlayer != null)
                {
                    previewPlayer.ShowWeapon(defaultWeapon);
                }

                UpdateButtons();
            }
        }
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

        // int currentCoins = PlayerPrefs.GetInt("Coins", 0);
        // if (currentCoins >= selectedWeapon.price)
        // {
        //     currentCoins -= selectedWeapon.price;
        //     PlayerPrefs.SetInt("Coins", currentCoins);

        PlayerPrefs.SetInt("WeaponBought_" + selectedWeapon.weaponName, 1);
        PlayerPrefs.Save();

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

        foreach (var item in weaponItems.Values)
        {
            bool isSelected = item.WeaponData.weaponName == selectedWeapon.weaponName;
            item.SetSelected(isSelected);
        }

        if (player != null)
        {
            var netChar = player.GetComponent<CharacterBase>();
            if (netChar != null && netChar.IsOwner)
            {
                // ✅ chỉ gọi ServerRpc, không gọi ChangeWeapon
                netChar.RequestChangeWeaponServerRpc(selectedWeapon.weaponType);
            }
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
                    var netChar = player.GetComponent<CharacterBase>();
                    if (netChar != null && netChar.IsOwner)
                    {
                        netChar.RequestChangeWeaponServerRpc(weapon.weaponType);
                        Debug.Log("Loaded selected weapon: " + weapon.weaponName);
                    }
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
        root.SetActive(false);
        InitSelectedWeapon();
        UIManager.Instance.OpenMainMenu();
    }

    private void OnDestroy()
    {
        foreach (var item in weaponItems.Values)
        {
            if (item != null)
            {
                item.OnWeaponSelected -= OnWeaponSelected;
            }
        }
    }
}