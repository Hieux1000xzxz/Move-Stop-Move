using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Unity.Netcode;

public class ShopCanvas : BaseCanvas
{
    [Header("UI References")]
    [SerializeField] private Transform weaponsGrid;
    [SerializeField] private GameObject weaponItemPrefab;
    [SerializeField] private Button buyWeaponButton;
    [SerializeField] private Button selectWeaponButton;
    [SerializeField] private Button closeButton;

    [Header("Shop Panels")]
    [SerializeField] private GameObject mainWeaponPanel;
    [SerializeField] private GameObject mainCharacterPanel;
    [SerializeField] private GameObject shopSelectionPanel;

    [Header("Weapons Data")]
    [SerializeField] private WeaponData[] weapons;
    [SerializeField] private Player player;
    [SerializeField] private PlayerPreview previewPlayer;

    [Header("Characters Data")]
    [SerializeField] private Transform charactersGrid;
    [SerializeField] private GameObject characterItemPrefab;
    [SerializeField] private CharacterData[] characters;
    [SerializeField] private Button buyCharacterButton;
    [SerializeField] private Button selectCharacterButton;
    [SerializeField] private PlayerSelectionSync playerSelectionSync;
    [SerializeField] private Button backCharacterButton;

    [Header("Coin UI")]
    [SerializeField] private TextMeshProUGUI coinText;

    private readonly Dictionary<string, WeaponItem> weaponItems = new();
    private readonly Dictionary<string, CharacterItem> characterItems = new();
    private WeaponData selectedWeapon;
    private CharacterData selectedCharacter;

    public bool CoinText() => coinText;
    private void Start()
    {
        // Wire buttons
        buyWeaponButton.onClick.AddListener(OnBuyWeapon);
        selectWeaponButton.onClick.AddListener(OnSelectWeapon);
        closeButton.onClick.AddListener(CloseShop);

        buyCharacterButton.onClick.AddListener(OnBuyCharacter);
        selectCharacterButton.onClick.AddListener(OnSelectCharacter);
        backCharacterButton.onClick.AddListener(CloseShop);

        // Init UI
        InitializeWeaponsGrid();
        InitializeCharactersGrid();

        InitSelectedWeapon();
        InitSelectedCharacter();

        UpdateCoinUI();
    }

    private void OnEnable() => UpdateCoinUI();

    //───────────────────────────────────────────────
    #region ─── WEAPON SHOP ────────────────────────────────────────────────

    private void InitializeWeaponsGrid()
    {
        foreach (Transform child in weaponsGrid)
            Destroy(child.gameObject);
        weaponItems.Clear();

        foreach (var weapon in weapons)
        {
            var obj = Instantiate(weaponItemPrefab, weaponsGrid);
            var item = obj.GetComponent<WeaponItem>();
            bool isBought = PlayerPrefs.GetInt("WeaponBought_" + weapon.weaponName, 0) == 1;
            bool isSelected = PlayerPrefs.GetString("SelectedWeapon", "") == weapon.weaponName;

            item.Initialize(weapon, isBought, isSelected);
            item.OnWeaponSelected += OnWeaponSelected;
            weaponItems.Add(weapon.weaponName, item);
        }
    }

    private void OnWeaponSelected(WeaponData weapon)
    {
        selectedWeapon = weapon;

        foreach (var item in weaponItems.Values)
            item.SetChosen(item.WeaponData == weapon);

        UpdateWeaponButtons();
        previewPlayer.ShowCharacterWithWeapon(selectedCharacter, selectedWeapon);
    }

    private void InitSelectedWeapon()
    {
        string selectedName = PlayerPrefs.GetString("SelectedWeapon", "");

        if (!string.IsNullOrEmpty(selectedName) && weaponItems.ContainsKey(selectedName))
            LoadSavedWeapon(selectedName);
        else
            LoadDefaultWeapon();
    }

    private void LoadSavedWeapon(string name)
    {
        WeaponItem item = weaponItems[name];
        selectedWeapon = item.WeaponData;
        UpdateWeaponSelectionUI(selectedWeapon);
        previewPlayer.ShowCharacterWithWeapon(selectedCharacter, selectedWeapon);
        UpdateWeaponButtons();
    }

    private void LoadDefaultWeapon()
    {
        if (weapons.Length == 0) return;
        WeaponData defaultWeapon = weapons[0];
        selectedWeapon = defaultWeapon;

        PlayerPrefs.SetString("SelectedWeapon", defaultWeapon.weaponName);
        PlayerPrefs.SetInt("WeaponBought_" + defaultWeapon.weaponName, 1);
        PlayerPrefs.Save();

        UpdateWeaponSelectionUI(defaultWeapon);
        previewPlayer.ShowCharacterWithWeapon(selectedCharacter, selectedWeapon);
        UpdateWeaponButtons();
    }

    private void OnBuyWeapon()
    {
        if (selectedWeapon == null) return;

        HandleBuyAction("WeaponBought_" + selectedWeapon.weaponName, selectedWeapon.weaponName, selectedWeapon.price);
        UpdateWeaponButtons();
    }

    private void OnSelectWeapon()
    {
        if (selectedWeapon == null) return;

        PlayerPrefs.SetString("SelectedWeapon", selectedWeapon.weaponName);
        PlayerPrefs.Save();

        UpdateWeaponSelectionUI(selectedWeapon);
        previewPlayer.ShowCharacterWithWeapon(selectedCharacter, selectedWeapon);
        UIManager.Instance.SendNotification($"✅ Selected {selectedWeapon.weaponName}!", 3);
        UpdateWeaponButtons();

        // Sync across network
        if (player != null && player.TryGetComponent(out CharacterBase netChar) && netChar.IsOwner)
            netChar.RequestChangeWeaponServerRpc(selectedWeapon.weaponType);
    }

    private void UpdateWeaponSelectionUI(WeaponData weapon)
    {
        foreach (var item in weaponItems.Values)
        {
            bool isSelected = item.WeaponData.weaponName == weapon.weaponName;
            item.SetSelected(isSelected);
        }
    }

    private void UpdateWeaponButtons()
    {
        if (selectedWeapon == null) return;
        bool isBought = PlayerPrefs.GetInt("WeaponBought_" + selectedWeapon.weaponName, 0) == 1;
        bool isSelected = PlayerPrefs.GetString("SelectedWeapon", "") == selectedWeapon.weaponName;

        buyWeaponButton.interactable = !isBought;
        selectWeaponButton.interactable = isBought && !isSelected;
    }

    #endregion
    //───────────────────────────────────────────────

    #region ─── CHARACTER SHOP ────────────────────────────────────────────────

    private void InitializeCharactersGrid()
    {
        foreach (Transform child in charactersGrid)
            Destroy(child.gameObject);
        characterItems.Clear();

        foreach (var character in characters)
        {
            var obj = Instantiate(characterItemPrefab, charactersGrid);
            var item = obj.GetComponent<CharacterItem>();
            bool isBought = PlayerPrefs.GetInt("CharacterBought_" + character.Name, 0) == 1;
            bool isSelected = PlayerPrefs.GetString("SelectedCharacter", "") == character.Name;

            item.Initialize(character, isBought, isSelected);
            item.OnClicked += OnCharacterSelected;
            characterItems.Add(character.Name, item);
        }
    }

    private void OnCharacterSelected(CharacterData character)
    {
        selectedCharacter = character;
        foreach (var item in characterItems.Values)
            item.SetChosen(item.Data == character);

        previewPlayer.ShowCharacterWithWeapon(selectedCharacter, selectedWeapon);
        UpdateCharacterButtons();
    }

    private void InitSelectedCharacter()
    {
        string selectedName = PlayerPrefs.GetString("SelectedCharacter", "");

        if (!string.IsNullOrEmpty(selectedName) && characterItems.ContainsKey(selectedName))
            LoadSavedCharacter(selectedName);
        else
            LoadDefaultCharacter();
    }

    private void LoadSavedCharacter(string name)
    {
        CharacterItem item = characterItems[name];
        selectedCharacter = item.Data;
        UpdateCharacterSelectionUI(selectedCharacter);
        previewPlayer.ShowCharacterWithWeapon(selectedCharacter, selectedWeapon);
        UpdateCharacterButtons();
    }

    private void LoadDefaultCharacter()
    {
        if (characters.Length == 0) return;
        CharacterData defaultChar = characters[0];
        selectedCharacter = defaultChar;

        PlayerPrefs.SetString("SelectedCharacter", defaultChar.Name);
        PlayerPrefs.SetInt("CharacterBought_" + defaultChar.Name, 1);
        PlayerPrefs.Save();

        UpdateCharacterSelectionUI(defaultChar);
        previewPlayer.ShowCharacterWithWeapon(selectedCharacter, selectedWeapon);
        UpdateCharacterButtons();
    }

    private void OnBuyCharacter()
    {
        if (selectedCharacter == null) return;

        HandleBuyAction("CharacterBought_" + selectedCharacter.Name, selectedCharacter.Name, selectedCharacter.Price);
        UpdateCharacterButtons();
    }

    private void OnSelectCharacter()
    {
        if (selectedCharacter == null) return;

        if (!IsCharacterBought(selectedCharacter))
        {
            UIManager.Instance.SendNotification("⚠️ You must buy this character first!", 2);
            return;
        }

        PlayerPrefs.SetString("SelectedCharacter", selectedCharacter.Name);
        PlayerPrefs.Save();

        UpdateCharacterSelectionUI(selectedCharacter);
        previewPlayer.ShowCharacterWithWeapon(selectedCharacter, selectedWeapon);
        UIManager.Instance.SendNotification($"✅ Selected {selectedCharacter.Name}!", 3);
        UpdateCharacterButtons();

        // Sync character across network
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            playerSelectionSync.SendSelectedCharacterServerRpc(selectedCharacter.Name);
    }

    private void UpdateCharacterSelectionUI(CharacterData character)
    {
        foreach (var item in characterItems.Values)
        {
            bool isSelected = item.Data == character;
            item.SetSelected(isSelected);
        }
    }

    private bool IsCharacterBought(CharacterData character)
        => PlayerPrefs.GetInt("CharacterBought_" + character.Name, 0) == 1;

    private void UpdateCharacterButtons()
    {
        if (selectedCharacter == null) return;
        bool isBought = IsCharacterBought(selectedCharacter);
        bool isSelected = PlayerPrefs.GetString("SelectedCharacter", "") == selectedCharacter.Name;

        buyCharacterButton.interactable = !isBought;
        selectCharacterButton.interactable = isBought && !isSelected;
    }

    #endregion
    //───────────────────────────────────────────────

    private bool TrySpendCoins(int price)
    {
        bool success = CoinManager.Instance.SpendCoin(price);
        if (!success)
            UIManager.Instance.SendNotification("Not enough coins!", 1);
        return success;
    }

    private void HandleBuyAction(string prefsKey, string itemName, int price)
    {
        if (!TrySpendCoins(price))
            return;

        PlayerPrefs.SetInt(prefsKey, 1);
        PlayerPrefs.Save();

        UIManager.Instance.SendNotification($"✅ Bought {itemName}!", 3);
        UpdateCoinUI();
    }

    public void UpdateCoinUI()
    {
        if (coinText == null) return;
        coinText.gameObject.SetActive(true);
        coinText.text = $"Coins: {CoinManager.Instance.GetShopCoins()}";
    }

    private void CloseShop()
    {
        InitSelectedCharacter();
        InitSelectedWeapon();
        
        mainWeaponPanel.SetActive(false);
        mainCharacterPanel.SetActive(false);
        shopSelectionPanel.SetActive(false);
        root.SetActive(false);
        coinText.gameObject.SetActive(false);
        
        UpdateCoinUI();
        UIManager.Instance.OpenMainMenu();
    }

    public void ShowWeaponShop()
    {
        mainWeaponPanel.SetActive(true);
        mainCharacterPanel.SetActive(false);
        shopSelectionPanel.SetActive(false);
        
        UpdateCoinUI();
    }

    public void ShowCharacterShop()
    {
        mainWeaponPanel.SetActive(false);
        mainCharacterPanel.SetActive(true);
        shopSelectionPanel.SetActive(false);
        
        UpdateCoinUI();
    }

    private void OnDestroy()
    {
        foreach (var item in weaponItems.Values)
            if (item != null)
                item.OnWeaponSelected -= OnWeaponSelected;
    }
}
