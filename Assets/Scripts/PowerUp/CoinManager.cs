using UnityEngine;
using TMPro;

public class CoinManager : Singleton<CoinManager>
{
    [Header("Coin Settings")]
    [SerializeField] private TMP_Text coinText;

    private const string COIN_KEY = "ShopCoins";

    private int sessionCoins = 0; 
    private int shopCoins = 0;    
    
    public int GetShopCoins() => shopCoins;
    public int GetSessionCoins() => sessionCoins;
    public int GetTotalCoins() => shopCoins + sessionCoins;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        shopCoins = PlayerPrefs.GetInt(COIN_KEY, 0);
        sessionCoins = 0;

    }

    private void Start()
    {
        UpdateCoinUI();
    }

    public void AddCoin(int amount)
    {
        sessionCoins += amount;

        UpdateCoinUI();
    }

    public void CommitSessionCoins()
    {
        shopCoins += sessionCoins;
        sessionCoins = 0;
        SaveCoins();
        PlayerPrefs.Save();
        UpdateCoinUI();
    }

    public bool SpendCoin(int amount)
    {
        if (shopCoins >= amount)
        {
            shopCoins -= amount;
            SaveCoins();
            UpdateCoinUI();
            return true;
        }

        return false;
    }

    private void SaveCoins()
    {
        PlayerPrefs.SetInt(COIN_KEY, shopCoins);
        PlayerPrefs.Save();

    }

    public static int LoadCoinsFromPrefs()
    {
        int coins = PlayerPrefs.GetInt(COIN_KEY, 0);
        return coins;
    }

    private void UpdateCoinUI()
    {
        if (coinText != null)
            coinText.text = $"Coins: {sessionCoins}";
    }
}
