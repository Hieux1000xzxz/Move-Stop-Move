using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ShopCanvas : BaseCanvas
{
    [Header("UI References")] [SerializeField]
    private Transform weaponsGrid;

    [SerializeField] private GameObject weaponItemPrefab;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button selectButton;
    [SerializeField] private Button closeButton;

    [Header("Weapons Data")] [SerializeField]
    private WeaponData[] weapons;

    [SerializeField] private Player player;
    [SerializeField] private PlayerPreview previewPlayer;

    private WeaponData selectedWeapon;
    private Dictionary<string, WeaponItem> weaponItems = new Dictionary<string, WeaponItem>();

    [SerializeField] private TextMeshProUGUI coinText;

    private void Awake()
    {
        if (coinText != null)
            coinText.gameObject.SetActive(false);
    }

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

        if (HasValidSelectedWeapon(selectedWeaponName))
        {
            SetExistingWeapon(selectedWeaponName);
        }
        else
        {
            SetDefaultWeapon();
        }
    }

    private bool HasValidSelectedWeapon(string weaponName)
    {
        return !string.IsNullOrEmpty(weaponName) && weaponItems.ContainsKey(weaponName);
    }

    private void SetExistingWeapon(string weaponName)
    {
        WeaponItem selectedItem = weaponItems[weaponName];
        selectedWeapon = selectedItem.WeaponData;

        UpdateWeaponItems(selectedItem);
        UpdatePreview(selectedWeapon);
        UpdateButtons();
    }

    private void SetDefaultWeapon()
    {
        if (weapons == null || weapons.Length == 0)
            return;

        WeaponData defaultWeapon = weapons[0];
        selectedWeapon = defaultWeapon;

        SaveDefaultWeapon(defaultWeapon);
        UpdateDefaultWeaponItems(defaultWeapon);
        UpdatePreview(defaultWeapon);
        UpdateButtons();
    }

    private void SaveDefaultWeapon(WeaponData defaultWeapon)
    {
        PlayerPrefs.SetString("SelectedWeapon", defaultWeapon.weaponName);
        PlayerPrefs.SetInt("WeaponBought_" + defaultWeapon.weaponName, 1);
        PlayerPrefs.Save();
    }

    private void UpdateWeaponItems(WeaponItem selectedItem)
    {
        foreach (var item in weaponItems.Values)
        {
            bool isSelected = item == selectedItem;
            item.SetChosen(isSelected);
            item.SetSelected(isSelected);
        }
    }

    private void UpdateDefaultWeaponItems(WeaponData defaultWeapon)
    {
        foreach (var item in weaponItems.Values)
        {
            bool isSelected = item.WeaponData.weaponName == defaultWeapon.weaponName;
            item.SetBought(isSelected);
            item.SetSelected(isSelected);
            item.SetChosen(isSelected);
        }
    }

    private void UpdatePreview(WeaponData weapon)
    {
        if (previewPlayer != null)
            previewPlayer.ShowWeapon(weapon);
    }

    private void InitializeWeaponsGrid()
    {
        ClearWeaponGrid();
        weaponItems.Clear();

        foreach (WeaponData weapon in weapons)
        {
            CreateWeaponItem(weapon);
        }
    }

    private void ClearWeaponGrid()
    {
        foreach (Transform child in weaponsGrid)
        {
            Destroy(child.gameObject);
        }
    }

    private void CreateWeaponItem(WeaponData weapon)
    {
        GameObject weaponItemObj = Instantiate(weaponItemPrefab, weaponsGrid);

        if (!weaponItemObj.TryGetComponent(out WeaponItem weaponItem) || weaponItem == null)
            return;

        bool isBought = IsWeaponBought(weapon.weaponName);
        bool isSelected = IsWeaponSelected(weapon.weaponName);

        InitializeWeaponItem(weaponItem, weapon, isBought, isSelected);
    }

    private bool IsWeaponBought(string weaponName)
    {
        return PlayerPrefs.GetInt("WeaponBought_" + weaponName, 0) == 1;
    }

    private bool IsWeaponSelected(string weaponName)
    {
        return PlayerPrefs.GetString("SelectedWeapon", "") == weaponName;
    }

    private void InitializeWeaponItem(WeaponItem item, WeaponData data, bool isBought, bool isSelected)
    {
        item.Initialize(data, isBought, isSelected);
        item.OnWeaponSelected += OnWeaponSelected;

        weaponItems.Add(data.weaponName, item);

        UpdateButtonState(isBought, isSelected);
    }

    private void UpdateButtonState(bool isBought, bool isSelected)
    {
        if (buyButton != null)
            buyButton.interactable = !isBought;

        if (selectButton != null)
            selectButton.interactable = isBought && isSelected;
    }

    private void OnEnable()
    {
        UpdateCoinUI();
    }

    public void UpdateCoinUI()
    {
        if (coinText != null)
        {
            coinText.gameObject.SetActive(true);
            coinText.text = $"Coins: {CoinManager.Instance.GetShopCoins()}";
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

        int price = selectedWeapon.price;

        bool success = CoinManager.Instance.SpendCoin(price);
        if (!success)
        {
            UIManager.Instance.SendNotification("Not enough coins to buy this weapon!", 1);
            return;
        }

        PlayerPrefs.SetInt("WeaponBought_" + selectedWeapon.weaponName, 1);
        PlayerPrefs.Save();

        weaponItems[selectedWeapon.weaponName].SetBought(true);
        UpdateButtons();
        UpdateCoinUI();
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
            CharacterBase netChar = player.GetComponent<CharacterBase>();
            if (netChar != null && netChar.IsOwner)
            {
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
                    player.ChangeWeapon(weapon.weaponType);
                    return;
                }
            }
        }
    }

    private void CloseShop()
    {
        root.SetActive(false);
        coinText.gameObject.SetActive(false);
        InitSelectedWeapon();
        UIManager.Instance.OpenMainMenu();
    }

    private void OnDestroy()
    {
        foreach (var item in weaponItems.Values)
        {
            if (item != null)
                item.OnWeaponSelected -= OnWeaponSelected;
        }
    }
}