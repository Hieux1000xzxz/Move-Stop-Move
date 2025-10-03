using UnityEngine;
using TMPro;

public class CoinManager : Singleton<CoinManager>
{
    [Header("Coin Settings")]
    [SerializeField] private int totalCoins = 0;

    [Header("UI")]
    [SerializeField] private TMP_Text coinText;
    public int GetTotalCoins() => totalCoins;

    protected override void Awake() 
    {
        base.Awake();
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
            UpdateCoinUI();
            return true;
        }
        return false;
    }

    private void UpdateCoinUI()
    {
        if (coinText != null)
            coinText.text = $"Coins: {totalCoins}";
    }

}