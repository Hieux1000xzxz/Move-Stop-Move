using System.Collections;
using UnityEngine;

public partial class ConnectionCanvas
{
    #region SinglePlayerMode
    private void OnSinglePlayerClicked()
    {
        PrepareSinglePlayerData();
        SetupLocalHost();
        StartCoroutine(StartSinglePlayerGame());
        modePanel.SetActive(false);
    }
    private void PrepareSinglePlayerData()
    {
        isSinglePlayerMode = true;
        localUserName = PlayerPrefs.GetString("PlayerName", DEFAULT_PLAYER_NAME);
        currentLobbyId = "SINGLE";
    }
    private void SetupLocalHost()
    {
        if (networkManager == null) return;
        transport.SetConnectionData("127.0.0.1", 7777);
        networkManager.StartHost();
    }
    private IEnumerator StartSinglePlayerGame()
    {
        yield return null;
        GameManager.Instance.StartGame();
        GameManager.Instance.StartPowerupSpawning();

        if (gameplayCanvas != null)
            gameplayCanvas.Init(this, "SINGLE", localUserName);
    }
    #endregion
}
