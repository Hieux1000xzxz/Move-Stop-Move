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
        weaponPriceText.text = "Price: " + weapon.price;
        weaponDescriptionText.text = weapon.description;

        if (PlayerPrefs.GetInt("WeaponBought_" + weapon.weaponName, 0) == 1)
        {
            buyButton.interactable = false;
            selectButton.interactable = true;
        }
        else
        {
            buyButton.interactable = true;
            selectButton.interactable = false;
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
        // Tạm thời chỉ lưu trạng thái đã mua
        PlayerPrefs.SetInt("WeaponBought_" + weapon.weaponName, 1);
        PlayerPrefs.Save();

        UpdateUI();
    }

    private void OnSelectWeapon()
    {
        WeaponData weapon = weapons[currentIndex];

        // Lưu weapon đã chọn
        PlayerPrefs.SetString("SelectedWeapon", weapon.weaponName);
        PlayerPrefs.Save();
        Debug.Log("Selected weapon: " + weapon.weaponName);

        if (player != null)
        {
            player.ChangeWeapon(weapon.weaponType);
        }
        else
        {
            Debug.LogWarning("Player not found in scene!");
        }
    }


    private void CloseShop()
    {
        gameObject.SetActive(false);
        UIManager.Instance.OpenMainMenu();
    }
}
