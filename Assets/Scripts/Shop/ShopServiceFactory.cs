public static class ShopServiceFactory
{
    public static IShopService<CharacterData> CreateCharacterService()
    {
        return new CharacterShopService();
    }

    // Nếu sau này có thêm vũ khí:
    // public static IShopService<WeaponData> CreateWeaponService() => new WeaponShopService();
}