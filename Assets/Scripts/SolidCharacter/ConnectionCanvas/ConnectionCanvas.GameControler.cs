using System.Collections;
using UnityEngine;

public partial class ConnectionCanvas
{
    #region GameStart/Exit
        private void OnStartGameClicked()
        {
            if (networkManager.IsHost)
            {
                StartCoroutine(StartGameSequence());
            }
        }
    
        private IEnumerator StartGameSequence()
        {
            startGameButton.interactable = false;
    
            yield return StartCoroutine(StartGameOnServerRoutine());
    
            GameManager.Instance.StartGame();
            GameManager.Instance.StartGameClientRpc();
            GameManager.Instance.StartPowerupSpawning();
    
            if (gameplayCanvas != null)
            {
                gameplayCanvas.Init(this, currentLobbyId, localUserName);
            }
        }
    
        private void OnExitClicked()
        {
            HandleExitLogic();
        }
    
        public void HandleExitLogic()
        {
            if (isSinglePlayerMode)
            {
                if (networkManager != null && networkManager.IsListening)
                {
                    networkManager.Shutdown();
                }
                GameManager.Instance.StopPowerupSpawning();
                ResetState();
                ResetNetworkManager();
                modePanel.SetActive(true);
                return;
            }
    
            isIntentionalDisconnect = true;
            isCreatingRoom = false;
            isJoiningRoom = false;
            if (clientHeartbeatRoutine != null)
            {
                StopCoroutine(clientHeartbeatRoutine);
                clientHeartbeatRoutine = null;
            }
    
            if (networkManager == null) return;
    
            bool wasHost = networkManager.IsHost;
            bool wasClient = networkManager.IsClient;
            string playerId = PlayerPrefs.GetString("PlayerId", "");
            string lobbyId = currentLobbyId;
    
            if (networkManager.IsListening)
            {
                networkManager.Shutdown();
            }
    
            ResetNetworkManager();
    
            if (wasHost)
            {
                if (heartbeatRoutine != null) StopCoroutine(heartbeatRoutine);
                if (!string.IsNullOrEmpty(lobbyId))
                {
                    StartCoroutine(DeleteLobbyRoutine(lobbyId));
                }
            }
            else if (wasClient)
            {
                if (!string.IsNullOrEmpty(lobbyId) && !string.IsNullOrEmpty(playerId))
                {
                    var user = new UserInfo { userId = playerId, userName = localUserName };
                    StartCoroutine(LeaveLobbyRoutine(lobbyId, user));
                }
            }
    
            if (mainCamera != null && playerPreview != null)
            {
                mainCamera.Follow = playerPreview.transform;
                mainCamera.LookAt = playerPreview.transform;
            }
    
            GameManager.Instance.StopPowerupSpawning();
            ResetState();
            ResetUIState();
            RefreshLobbyList();
    
            isIntentionalDisconnect = false;
        }
    
        private void ResetState()
        {
            currentLobbyId = string.Empty;
            currentRelayJoinCode = string.Empty;
            isSinglePlayerMode = false;
            currentLobbyInfo = null;
            isReady = false;
        }
    
        private void ResetUIState()
        {
            lobbyPanel.SetActive(false);
            if (joinByIdPanel != null) joinByIdPanel.SetActive(false);
            if (playerInfoPanel != null) playerInfoPanel.SetActive(false);
            if (settingPanel != null) settingPanel.SetActive(false);
            mainPanel.SetActive(true);
            EnableMainPanelButton();
            modePanel.SetActive(false);
            isCreatingRoom = false;
            isJoiningRoom = false;
        }
        #endregion
    
}
