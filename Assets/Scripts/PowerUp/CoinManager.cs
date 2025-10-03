using UnityEngine;
using TMPro;

public class CoinManager : Singleton<CoinManager>
{
    [Header("Coin Settings")]
    [SerializeField] private int totalCoins = 0;

    [Header("UI")]
    [SerializeField] private TMP_Text coinText;
    
    private const string COIN_KEY = "ShopCoins";
    
    public int GetTotalCoins() => totalCoins;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
        
        totalCoins = PlayerPrefs.GetInt(COIN_KEY, 0);
    }
    
    private void Start()
    {
        UpdateCoinUI();
    }

    public void AddCoin(int amount)
    {
        totalCoins += amount;
        UpdateCoinUI();
    }

    public bool SpendCoin(int amount)
    {
        if (totalCoins >= amount)
        {
            totalCoins -= amount;
            SaveCoins();
            UpdateCoinUI();
            return true;
        }
        return false;
    }
    
    private void SaveCoins()
    {
        PlayerPrefs.SetInt(COIN_KEY, totalCoins);
        PlayerPrefs.Save();
    }
    
    private void UpdateCoinUI()
    {
        if (coinText != null)
            coinText.text = $"Coins: {totalCoins}";
    }

}