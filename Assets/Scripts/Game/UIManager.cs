using UnityEngine;
using TMPro;
using Unity.Netcode;

public class UIManager : Singleton<UIManager>
{
    [Header("AI Counter")]
    [SerializeField] private TextMeshProUGUI aiCounterText;
    [SerializeField] private string displayFormat = "Enemies Left: {0}";

    [Header("Canvases")]
    [SerializeField] private MainMenuCanvas mainMenuCanvas;
    [SerializeField] private ShopCanvas shopCanvas;
    [SerializeField] private ConnectionCanvas connectionCanvas;
    [SerializeField] private LoadingCanvas loadingCanvas;
    [SerializeField] private NotificationCanvas notificationCanvas;
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

    public void OpenUI(BaseCanvas canvas)
    {
        if (canvas == null) return;

        CloseAllUI();
        canvas.Show();
    }

    public void CloseUI(BaseCanvas canvas)
    {
        if (canvas != null) canvas.Hide();
    }

    public void CloseAllUI()
    {
        if (mainMenuCanvas != null) mainMenuCanvas.Hide();
        if (shopCanvas != null) shopCanvas.Hide();
        if (connectionCanvas != null) connectionCanvas.Hide();
    }
    public void StartGame()
    {
        mainMenuCanvas.Hide();
        shopCanvas.Hide();

        if(NetworkManager.Singleton.IsServer)
        {
            GameManager.Instance.StartGame();
            GameManager.Instance.StartGameClientRpc();
        }

        else
        {
            GameManager.Instance.RequestStartGameServerRpc();
        }
    }
    public void OpenNoti(BaseCanvas canvas)
    {
        canvas.Show();
    }
    public void SendNotification(string message)
    {
        if (notificationCanvas != null)
        {
            notificationCanvas.SetText(message);
        }
    }
    public void OpenLoadingCanvas() => OpenUI(loadingCanvas);
    public void OpenMainMenu() => OpenUI(mainMenuCanvas);
    public void OpenShop() => OpenUI(shopCanvas);
    public void OpenConnection() => OpenUI(connectionCanvas);
    public void OpenNotification() => OpenNoti(notificationCanvas);

    public void CloseMainMenu() => CloseUI(mainMenuCanvas);
    public void CloseShop() => CloseUI(shopCanvas);
    public void CloseNetwork() => CloseUI(connectionCanvas);
    public void CloseNotification() => CloseUI(notificationCanvas); 
}