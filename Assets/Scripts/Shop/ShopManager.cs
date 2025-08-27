using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopManager : BaseCanvas
{
    [SerializeField] private Transform shopPanel;
    [SerializeField] private GameObject weaponButtonPrefab;
    [SerializeField] private WeaponData[] weaponList;
    [SerializeField] private Player player;

    private void Start()
    {

        foreach (var weapon in weaponList)
        {
            GameObject buttonObj = Instantiate(weaponButtonPrefab, shopPanel);

            Image icon = buttonObj.transform.Find("Icon").GetComponent<Image>();
            TMP_Text nameText = buttonObj.transform.Find("NameText").GetComponent<TMP_Text>();
            TMP_Text priceText = buttonObj.transform.Find("PriceText").GetComponent<TMP_Text>();
            Button btn = buttonObj.GetComponent<Button>();

            icon.sprite = weapon.weaponIcon;
            nameText.text = weapon.weaponName;
            priceText.text = "$" + weapon.price;

            btn.onClick.AddListener(() =>
            {
                BuyWeapon(weapon);
            });
        }
    }

    private void BuyWeapon(WeaponData weapon)
    {

        player.ChangeWeapon(weapon.weaponType);

        if (player.currentWeaponPublic != null)
        {
            player.currentWeaponPublic.ApplyData(weapon);
        }

        Debug.Log("Player equipped: " + weapon.weaponName);
    }
}
