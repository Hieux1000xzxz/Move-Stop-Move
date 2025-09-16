using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopCanvas : BaseCanvas
{
    [Header("UI References")]
    [SerializeField] private Image weaponImage;
    [SerializeField] private TextMeshProUGUI weaponNameText;
    [SerializeField] private TextMeshProUGUI weaponPriceText;
    [SerializeField] private TextMeshProUGUI weaponDescriptionText;

    [SerializeField] private Button buyButton;
    [SerializeField] private Button selectButton;
    [SerializeField] private Button leftArrowButton;
    [SerializeField] private Button rightArrowButton;
    [SerializeField] private Button closeButton;

    [Header("Weapons Data")]
    [SerializeField] private WeaponData[] weapons;
    [SerializeField] private Player player;

    [SerializeField] private PlayerPreview previewPlayer;

    private int currentIndex = 0;

    private void Start()
    {
        buyButton.onClick.AddListener(OnBuyWeapon);
        selectButton.onClick.AddListener(OnSelectWeapon);
        leftArrowButton.onClick.AddListener(ShowPreviousWeapon);
        rightArrowButton.onClick.AddListener(ShowNextWeapon);
        closeButton.onClick.AddListener(CloseShop);
        UpdateUI();

    }

    private void UpdateUI()
    {
        if (weapons.Length == 0) return;

        WeaponData weapon = weapons[currentIndex];

        weaponImage.sprite = weapon.weaponIcon;
        weaponNameText.text = weapon.weaponName;
        weaponPriceText.text = "" + weapon.price;
        weaponDescriptionText.text = weapon.description;

        bool isBought = PlayerPrefs.GetInt("WeaponBought_" + weapon.weaponName, 0) == 1;
        string selectedWeapon = PlayerPrefs.GetString("SelectedWeapon", "");

        if (isBought)
        {
            buyButton.interactable = false;

            if (selectedWeapon == weapon.weaponName)
            {
                selectButton.interactable = false;
            }
            else
            {
                selectButton.interactable = true;
            }
        }
        else
        {
            buyButton.interactable = true;
            selectButton.interactable = false;
        }

        if (previewPlayer != null)
        {
            previewPlayer.ShowWeapon(weapon);
        }

    }


    private void ShowPreviousWeapon()
    {
        currentIndex--;
        if (currentIndex < 0) currentIndex = weapons.Length - 1;
        UpdateUI();
    }

    private void ShowNextWeapon()
    {
        currentIndex++;
        if (currentIndex >= weapons.Length) currentIndex = 0;
        UpdateUI();
    }

    private void OnBuyWeapon()
    {
        WeaponData weapon = weapons[currentIndex];
        PlayerPrefs.SetInt("WeaponBought_" + weapon.weaponName, 1);
        PlayerPrefs.Save();

        UpdateUI();
    }

    private void OnSelectWeapon()
    {
        WeaponData weapon = weapons[currentIndex];

        PlayerPrefs.SetString("SelectedWeapon", weapon.weaponName);
        PlayerPrefs.Save();
        Debug.Log("Selected weapon: " + weapon.weaponName);

        if (player != null)
        {
            CharacterBase netChar = player.GetComponent<CharacterBase>();
            if (netChar != null && netChar.IsOwner)
            {
                netChar.RequestChangeWeaponServerRpc(weapon.weaponType);
            }
        }

        else
        {
            Debug.LogWarning("Player not found in scene!");
        }
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

}
