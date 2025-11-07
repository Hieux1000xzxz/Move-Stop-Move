using System.Collections;
using UnityEngine;

public partial class ConnectionCanvas
{
    #region SinglePlayerMode
    private void OnSinglePlayerClicked()
    {
        isSinglePlayerMode = true;
        localUserName = PlayerPrefs.GetString("PlayerName", DEFAULT_PLAYER_NAME);
        currentLobbyId = "SINGLE";

        if (networkManager != null)
        {
            transport.SetConnectionData("127.0.0.1", 7777);
            networkManager.StartHost();
        }

        StartCoroutine(StartSinglePlayerGame());
        modePanel.SetActive(false);
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
