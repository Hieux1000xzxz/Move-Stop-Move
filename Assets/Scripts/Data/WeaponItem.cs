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
    [SerializeField] private GameObject chooseHighlight;   
    [SerializeField] private GameObject selectedHighlight;
    [SerializeField] private GameObject boughtOverlay; 
    [SerializeField] private Button itemButton;

    public WeaponData WeaponData { get; private set; }
    public event Action<WeaponData> OnWeaponSelected;

    public void Initialize(WeaponData weapon, bool isBought, bool isSelected)
    {
        WeaponData = weapon;

        weaponIcon.sprite = weapon.weaponIcon;
        weaponNameText.text = weapon.weaponName;
        priceText.text = weapon.price.ToString();

        boughtOverlay.SetActive(false);
        priceText.gameObject.SetActive(true);
        
        SetBought(isBought);
        SetSelected(isSelected);
        SetChosen(false); 

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

    public void SetSelected(bool isSelected)
    {
        selectedHighlight.SetActive(isSelected);
    }

    public void SetChosen(bool isChosen)
    {
        chooseHighlight.SetActive(isChosen);
    }

    private void OnDestroy()
    {
        itemButton.onClick.RemoveListener(OnItemClicked);
    }
}
