using System.Collections;

public partial class ConnectionCanvas
{
    #region OnlineMode-Entry
    private async void OnOnlineClicked()
    {
        isSinglePlayerMode = false;
        SendNotification("Checking connection...", 2);
        if (!isUnityServicesInitialized)
            await InitializeUnityServices();

        StartCoroutine(CheckNetworkAndShowMainPanel());
    }

    private IEnumerator CheckNetworkAndShowMainPanel()
    {
        bool serverAvailable = false;
        yield return StartCoroutine(EnsureServerAvailable(result => serverAvailable = result));

        if (serverAvailable && isUnityServicesInitialized)
        {
            UIManager.Instance.CloseNotification();
            modePanel.SetActive(false);
            mainPanel.SetActive(true);
            EnableMainPanelButton();
        }
        else
        {
            string message = !isUnityServicesInitialized
                ? "Unable to connect to online services. Please check your internet connection and try again."
                : "Connection failed. Please check your internet connection.";
            SendNotification(message, 1);
        }
    }
    #endregion
}
