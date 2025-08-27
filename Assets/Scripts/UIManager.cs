using UnityEngine;
using TMPro;

public class UIManager : Singleton<UIManager>
{
    [Header("AI Counter")]
    [SerializeField] private TextMeshProUGUI aiCounterText;
    [SerializeField] private string displayFormat = "Enemies Left: {0}";

    [Header("Canvases")]
    [SerializeField] private MainMenuCanvas mainMenuCanvas;
    [SerializeField] private ShopCanvas shopCanvas;

    private void Update()
    {
        UpdateAICounter();
    }

    private void UpdateAICounter()
    {
        if (GameManager.Instance == null || aiCounterText == null) return;

        int remainingAI = GameManager.Instance.GetRemainingQuota();
        int activeAI = GameManager.Instance.GetActiveAICount();
        int totalRemaining = remainingAI + activeAI;

        if (totalRemaining <= 0)
        {
            aiCounterText.text = "All Enemies Defeated!";
            aiCounterText.color = Color.green;
        }
        else
        {
            aiCounterText.text = string.Format(displayFormat, totalRemaining);
            aiCounterText.color = totalRemaining <= 10 ? Color.red : Color.white;
        }
    }

    // Mở UI và đóng các UI khác
    public void OpenUI(BaseCanvas canvas)
    {
        if (canvas == null) return;

        CloseAllUI();
        canvas.Show();
    }

    // Đóng UI cụ thể
    public void CloseUI(BaseCanvas canvas)
    {
        if (canvas != null) canvas.Hide();
    }

    // Đóng tất cả UI
    public void CloseAllUI()
    {
        if (mainMenuCanvas != null) mainMenuCanvas.Hide();
        if (shopCanvas != null) shopCanvas.Hide();
    }

    // Các hàm cụ thể cho từng UI
    public void OpenMainMenu() => OpenUI(mainMenuCanvas);
    public void OpenShop() => OpenUI(shopCanvas);

    public void CloseMainMenu() => CloseUI(mainMenuCanvas);
    public void CloseShop() => CloseUI(shopCanvas);
}