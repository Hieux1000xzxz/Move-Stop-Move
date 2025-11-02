public interface IPreviewStrategy<T> where T : IShopItemData
{
    void ShowPreview(T data);
}