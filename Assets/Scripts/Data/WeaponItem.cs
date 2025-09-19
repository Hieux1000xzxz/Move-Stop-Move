using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class WeaponItem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image weaponIcon;
    [SerializeField] private TextMeshProUGUI weaponNameText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private GameObject chooseHighlight;   // highlight khi chỉ chọn
    [SerializeField] private GameObject selectedHighlight; // highlight khi đang dùng
    [SerializeField] private GameObject boughtOverlay;     // overlay đã mua
    [SerializeField] private Button itemButton;

    public WeaponData WeaponData { get; private set; }
    public event Action<WeaponData> OnWeaponSelected;

    public void Initialize(WeaponData weapon, bool isBought, bool isSelected)
    {
        WeaponData = weapon;

        weaponIcon.sprite = weapon.weaponIcon;
        weaponNameText.text = weapon.weaponName;
        priceText.text = weapon.price.ToString();

        SetBought(isBought);
        SetSelected(isSelected);
        SetChosen(false); // ban đầu chưa chọn

        itemButton.onClick.AddListener(OnItemClicked);
    }

    private void OnItemClicked()
    {
        OnWeaponSelected?.Invoke(WeaponData);
    }

    public void SetBought(bool isBought)
    {
        boughtOverlay.SetActive(isBought);
        priceText.gameObject.SetActive(!isBought);
    }

    // highlight khi bấm "Select" → đang sử dụng
    public void SetSelected(bool isSelected)
    {
        selectedHighlight.SetActive(isSelected);
    }

    // highlight khi click vào item trong shop
    public void SetChosen(bool isChosen)
    {
        chooseHighlight.SetActive(isChosen);
    }

    private void OnDestroy()
    {
        itemButton.onClick.RemoveListener(OnItemClicked);
    }
}
