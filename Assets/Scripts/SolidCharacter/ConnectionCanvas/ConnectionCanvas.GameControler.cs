using System.Collections;
using UnityEngine;

public partial class ConnectionCanvas
{
    #region GameStart/Exit
        private void OnExitClicked() =>HandleExitLogic();
        private void OnStartGameClicked()
        {
            if (networkManager.IsHost)
                StartCoroutine(StartGameSequence());
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
        public void HandleExitLogic()
        {
            if (isSinglePlayerMode)
            {
                ExitSinglePlayer();
                return;
            }
            PrepareForExit();
            ExecuteNetworkExit();
            RestoreMainMenuUI();
        }
        private void ExitSinglePlayer()
        {
            if (networkManager != null && networkManager.IsListening)
                networkManager.Shutdown();

            GameManager.Instance.StopPowerupSpawning();
            ResetState();
            ResetNetworkManager();
            modePanel.SetActive(true);
        }
        private void PrepareForExit()
        {
            isIntentionalDisconnect = true;
            isCreatingRoom = false;
            isJoiningRoom = false;

            if (clientHeartbeatRoutine != null)
            {
                StopCoroutine(clientHeartbeatRoutine);
                clientHeartbeatRoutine = null;
            }
        }
        private void ExecuteNetworkExit()
        {
            if (networkManager == null) return;

            bool wasHost = networkManager.IsHost;
            bool wasClient = networkManager.IsClient;
            string playerId = PlayerPrefs.GetString("PlayerId", "");
            string lobbyId = currentLobbyId;

            if (networkManager.IsListening)
                networkManager.Shutdown();

            ResetNetworkManager();
            StopHeartbeat();

            if (wasHost)
                TryDeleteLobby(lobbyId);
            else if (wasClient)
                TryLeaveLobby(lobbyId, playerId);
        }
        private void StopHeartbeat()
        {
            if (heartbeatRoutine != null)
            {
                StopCoroutine(heartbeatRoutine);
                heartbeatRoutine = null;
            }
        }

        private void TryDeleteLobby(string lobbyId)
        {
            if (!string.IsNullOrEmpty(lobbyId))
                StartCoroutine(DeleteLobbyRoutine(lobbyId));
        }

        private void TryLeaveLobby(string lobbyId, string playerId)
        {
            if (string.IsNullOrEmpty(lobbyId) || string.IsNullOrEmpty(playerId)) return;

            var user = new UserInfo { userId = playerId, userName = localUserName };
            StartCoroutine(LeaveLobbyRoutine(lobbyId, user));
        }

        private void RestoreMainMenuUI()
        {
            if (mainCamera != null && playerPreview != null)
            {
                mainCamera.Follow = playerPreview.transform;
                mainCamera.LookAt = playerPreview.transform;
            }

            GameManager.Instance.StopPowerupSpawning();
            ResetState();
            SetUIState(true);
            SafeRefreshLobby();
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
        #endregion
    
}
