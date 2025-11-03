using UnityEngine;
using System.Collections.Generic;

public class CharacterShopService : IShopService<CharacterData>
{
    private readonly Dictionary<string, CharacterItem> characterItems;

    public CharacterShopService(Dictionary<string, CharacterItem> items)
    {
        characterItems = items;
    }

    public bool IsBought(CharacterData item)
    {
        return PlayerPrefs.GetInt("CharacterBought_" + item.Name, 0) == 1;
    }

    public bool IsSelected(CharacterData item)
    {
        return PlayerPrefs.GetString("SelectedCharacter", "") == item.Name;
    }

    public bool CanAfford(int cost)
    {
        return CoinManager.Instance.GetShopCoins() >= cost;
    }

    public void BuyItem(CharacterData item)
    {
        if (item == null) return;

        int currentCoins = CoinManager.Instance.GetShopCoins();
        if (currentCoins < item.Price)
            return;

        CoinManager.Instance.SpendCoin(item.Price);
        PlayerPrefs.SetInt("CharacterBought_" + item.Name, 1);
        PlayerPrefs.Save();
    }

    public void SelectItem(CharacterData item)
    {
        if (!IsBought(item)) return;

        PlayerPrefs.SetString("SelectedCharacter", item.Name);
        PlayerPrefs.Save();

        foreach (var kvp in characterItems)
        {
            bool isSelected = kvp.Key == item.Name;
            kvp.Value.SetSelected(isSelected);
            kvp.Value.SetChosen(isSelected);
        }
    }
}