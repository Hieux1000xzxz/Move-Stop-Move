public interface IShopService<T> where T : IShopItemData
{
    bool IsBought(T item);
    bool IsSelected(T item);
    bool CanAfford(int cost);
    void BuyItem(T item);
    void SelectItem(T item);
}