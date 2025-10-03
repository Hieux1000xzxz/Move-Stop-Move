using UnityEngine;
using TMPro;
using Unity.Netcode;

public class UIManager : Singleton<UIManager>
{
    [Header("AI Counter")]
    [SerializeField] private TextMeshProUGUI enemyCountText;
    [SerializeField] private string displayFormat = "Enemies Left: {0}";

    [Header("Canvases")]
    [SerializeField] private MainMenuCanvas mainMenuCanvas;
    [SerializeField] private ShopCanvas shopCanvas;
    [SerializeField] private ConnectionCanvas connectionCanvas;
    [SerializeField] private LoadingCanvas loadingCanvas;
    [SerializeField] private NotificationCanvas notificationCanvas;
    public void UpdateEnemyCount(int count)
    {
        if (count <= 0)
        {
            enemyCountText.text = "All Enemies Defeated!";
            enemyCountText.color = Color.green;
        }
        else
        {
            enemyCountText.text = string.Format(displayFormat, count);
            enemyCountText.color = count <= 10 ? Color.red : Color.white;
        }
    }

    public void HideCountText() 
    { 
        enemyCountText.text = string.Empty;
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
        if (NetworkManager.Singleton.IsServer)
        {
            GameManager.Instance.StartGame();
            GameManager.Instance.StartGameClientRpc();
        }
        else
        {
            GameManager.Instance.RequestStartGameServerRpc();
        }
    }

    public void OpenNoti(BaseCanvas canvas) => canvas.Show();

    public void SendNotification(string message, int type = 1)
    {
        if (notificationCanvas != null)
        {
            notificationCanvas.SetText(message);

            if (type == 1)
            {
                notificationCanvas.ShowMainPanel();
                notificationCanvas.ShowCloseButton();
                notificationCanvas.HideConfirmButton();
            }
            else if (type == 2)
            {
                notificationCanvas.ShowMainPanel();
                notificationCanvas.HideCloseButton();
                notificationCanvas.HideConfirmButton();
            }
            else if (type == 3)
            {
                notificationCanvas.ShowMainPanel();
                notificationCanvas.HideCloseButton();
                notificationCanvas.ShowConfirmButton();
            }
            else if (type == 4)
            {
                notificationCanvas.HideMainPanel();
                notificationCanvas.ShowToast(message);
                Invoke(nameof(CloseNotification), 1.5f);
            }
            notificationCanvas.Show();
        }
    }

   

    public NotificationCanvas BindNotification()
    {
        OpenNoti(notificationCanvas);
        return notificationCanvas;
    }

    public void HideClosreNotifiButton() 
    {
        if (notificationCanvas != null)
        {
            notificationCanvas.HideCloseButton();
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
