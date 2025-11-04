using UnityEngine;
using UnityEngine.UI;

public class ShopSelectionCanvas : BaseCanvas
{
    [Header("Buttons")]
    [SerializeField] private Button weaponShopButton;
    [SerializeField] private Button characterShopButton;
    [SerializeField] private Button backButton;
    private void Start()
    {
        weaponShopButton.onClick.AddListener(OpenWeaponShop);
        characterShopButton.onClick.AddListener(OpenCharacterShop);
        backButton.onClick.AddListener(BackToMainMenu);
    }

    private void OpenWeaponShop()
    {
        UIManager.Instance.OpenShop();
        UIManager.Instance.GetShopCanvas().ShowWeaponShop(); 
    }

    private void OpenCharacterShop()
    {
        UIManager.Instance.OpenShop();
        UIManager.Instance.GetShopCanvas().ShowCharacterShop(); 
    }


    private void BackToMainMenu()
    {
        UIManager.Instance.OpenMainMenu();
        Hide();
    }
}