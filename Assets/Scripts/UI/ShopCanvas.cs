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

    [Header("Shop Panels")]
    [SerializeField] private GameObject mainWeaponPanel;    // object MAIN
    [SerializeField] private GameObject mainCharacterPanel; // object MAIN2

    
    [Header("Weapons Data")]
    [SerializeField] private WeaponData[] weapons;
    [SerializeField] private Player player;
    [SerializeField] private PlayerPreview previewPlayer;

    
    [Header("Character Shop UI")]
    [SerializeField] private Transform charactersGrid;
    [SerializeField] private GameObject characterItemPrefab;
    [SerializeField] private CharacterData[] characters;

    [Header("Character Buttons")]
    [SerializeField] private Button buyCharacterButton;
    [SerializeField] private Button selectCharacterButton;
    [SerializeField] private GameObject shopSelectionPanel;

    private CharacterData selectedCharacter;
    private readonly Dictionary<string, CharacterItem> characterItems = new();

    
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
        buyCharacterButton.onClick.AddListener(OnBuyCharacter);
        selectCharacterButton.onClick.AddListener(OnSelectCharacter);

        InitializeWeaponsGrid();
        InitializeCharactersGrid();
        previewPlayer.ShowCharacter(characters[0]);
        InitSelectedWeapon();
    }
    
    public void InitSelectedWeapon()
    {
        string selectedWeaponName = PlayerPrefs.GetString("SelectedWeapon", "");

        if (!string.IsNullOrEmpty(selectedWeaponName) && weaponItems.ContainsKey(selectedWeaponName))
        {
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
            if (weapons.Length > 0)
            {
                WeaponData defaultWeapon = weapons[0];
                selectedWeapon = defaultWeapon;

                PlayerPrefs.SetString("SelectedWeapon", defaultWeapon.weaponName);
                PlayerPrefs.SetInt("WeaponBought_" + defaultWeapon.weaponName, 1); 
                PlayerPrefs.Save();

                foreach (var item in weaponItems.Values)
                {
                    bool isSelected = item.WeaponData.weaponName == defaultWeapon.weaponName;
                    item.SetBought(isSelected);   
                    item.SetSelected(isSelected); 
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
    
    private void InitializeCharactersGrid()
    {
        Debug.Log($"[CharacterShop] Initialize grid, count = {characters.Length}");

        foreach (Transform child in charactersGrid)
            Destroy(child.gameObject);

        characterItems.Clear();

        foreach (CharacterData character in characters)
        {
            if (character == null)
            {
                Debug.LogWarning("[CharacterShop] Null CharacterData found!");
                continue;
            }

            Debug.Log($"[CharacterShop] Creating item: {character.Name}");

            GameObject obj = Instantiate(characterItemPrefab, charactersGrid);
            CharacterItem item = obj.GetComponent<CharacterItem>();

            bool isBought = PlayerPrefs.GetInt("CharacterBought_" + character.Name, 0) == 1;
            bool isSelected = PlayerPrefs.GetString("SelectedCharacter", "") == character.Name;

            item.Initialize(character, isBought, isSelected);
            item.SetChosen(false);
            item.OnClicked += OnCharacterSelected;

            if (!characterItems.ContainsKey(character.Name))
                characterItems.Add(character.Name, item);
            else
                Debug.LogError($"[CharacterShop] Duplicate name found: {character.Name}");
        }
    }

    private void OnCharacterSelected(CharacterData character)
    {
        selectedCharacter = character;
        foreach (var item in characterItems.Values)
        {
            bool isSelected = item.Data == character;
            item.SetSelected(item.Data == character);
            item.SetChosen(isSelected);
        }
            
        previewPlayer.ShowCharacter(selectedCharacter);
        UpdateCharacterButtons();
    }

    private void OnBuyCharacter()
    {
        if (selectedCharacter == null) return;

        if (!CoinManager.Instance.SpendCoin(selectedCharacter.Price))
        {
            UIManager.Instance.SendNotification("Not enough coins to buy this character!", 1);
            return;
        }

        PlayerPrefs.SetInt("CharacterBought_" + selectedCharacter.Name, 1);
        PlayerPrefs.Save();

        characterItems[selectedCharacter.Name].SetBought(true);
        UpdateCharacterButtons();
        UpdateCoinUI();
    }

    private void OnSelectCharacter()
    {
        if (selectedCharacter == null) return;

        PlayerPrefs.SetString("SelectedCharacter", selectedCharacter.Name);
        PlayerPrefs.Save();

        foreach (var item in characterItems.Values)
            item.SetSelected(item.Data == selectedCharacter);

        UIManager.Instance.SendNotification($"Selected {selectedCharacter.Name}!", 4);
        UpdateCharacterButtons();
    }

    private void UpdateCharacterButtons()
    {
        if (selectedCharacter == null) return;

        bool isBought = PlayerPrefs.GetInt("CharacterBought_" + selectedCharacter.Name, 0) == 1;
        bool isSelected = PlayerPrefs.GetString("SelectedCharacter", "") == selectedCharacter.Name;

        buyButton.interactable = !isBought;
        selectButton.interactable = isBought && !isSelected;
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

    public void ShowWeaponShop()
    {
        if (mainWeaponPanel != null) mainWeaponPanel.SetActive(true);
        if (mainCharacterPanel != null) mainCharacterPanel.SetActive(false);
        if (shopSelectionPanel != null) shopSelectionPanel.SetActive(false);

    }

    public void ShowCharacterShop()
    {
        if (mainWeaponPanel != null) mainWeaponPanel.SetActive(false);
        if (mainCharacterPanel != null) mainCharacterPanel.SetActive(true);
        if (shopSelectionPanel != null) shopSelectionPanel.SetActive(false);
        InitializeCharactersGrid();
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