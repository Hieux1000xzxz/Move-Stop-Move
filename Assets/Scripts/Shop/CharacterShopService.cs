using UnityEngine;

public class CharacterShopService : IShopService<CharacterData>
{
    public bool IsBought(CharacterData item)
    {
        return PlayerPrefs.GetInt("CharBought_" + item.Name, 0) == 1;
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
        if (!CanAfford(item.Price))
        {
            UIManager.Instance.SendNotification("Không đủ xu để mua!", 1);
            return;
        }

        CoinManager.Instance.SpendCoin(item.Price);
        PlayerPrefs.SetInt("CharBought_" + item.Name, 1);
        PlayerPrefs.Save();

        UIManager.Instance.SendNotification($"Đã mua {item.Name}!", 4);
    }

    public void SelectItem(CharacterData item)
    {
        PlayerPrefs.SetString("SelectedCharacter", item.Name);
        PlayerPrefs.Save();

        UIManager.Instance.SendNotification($"Đã chọn {item.Name}!", 4);
    }
}