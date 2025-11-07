using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.Networking;

public partial class ConnectionCanvas
{
     #region Join
    public void ShowJoinByIdPanel()
    {
        if (isCreatingRoom || (lobbyPanel != null && lobbyPanel.activeSelf))
            return;
        
        CancelJoinProcess();
        
        if (joinByIdPanel != null)
        {
            joinByIdPanel.SetActive(true);
            lobbyIdInputField.text = string.Empty;
            DisableMainPanelButton();
            
            DisableAllJoinButtons();
            
            UIManager.Instance.CloseNotification();
        }
    }
    private void OnConfirmJoinById()
    {
        if (isCreatingRoom || isJoiningRoom) return;
        
        string lobbyId = lobbyIdInputField != null ? lobbyIdInputField.text.Trim() : "";
        
        if (!IsValidString(lobbyId, "Lobby ID"))
            return;
        if (joinByIdPanel != null) 
            joinByIdPanel.SetActive(false);
        DisableMainPanelButton();
        DisableAllJoinButtons();

        UIManager.Instance.CloseNotification();
        
        isJoiningRoom = true;
        
        joinLobbyCoroutine = StartCoroutine(JoinLobbyRoutine(lobbyId, localUserName));
    }
    public void JoinLobbyDirect(RelayLobbyInfo lobby)
    {
        if (isJoiningRoom || isCreatingRoom) return;
        if (lobby == null) return;
        
        isJoiningRoom = true;
        DisableMainPanelButton(); 
        DisableAllJoinButtons();
            
        ResetNetworkManager();
        joinLobbyCoroutine = StartCoroutine(JoinLobbyRoutine(lobby.lobbyId, localUserName));
    }
    private IEnumerator JoinLobbyRoutine(string lobbyId, string playerName)
    {
        isJoiningRoom = true;
        try
        {
            using (var checkWww = UnityWebRequest.Get($"{SERVER_URL}/find/{lobbyId}"))
            {
                checkWww.timeout = 10;
                yield return checkWww.SendWebRequest();

                if (checkWww.result != UnityWebRequest.Result.Success)
                {
                    SendNotification("Failed to join lobby. Please try again.", 1);
                    HandleExitLogic();
                    yield break;
                }

                RelayLobbyInfo lobbyCheck = JsonUtility.FromJson<RelayLobbyInfo>(checkWww.downloadHandler.text);
                if (lobbyCheck == null || lobbyCheck.isGameStarted)
                {
                    SendNotification("Lobby not found or game already started.", 1);
                    yield break;
                }
            }

            string playerId = PlayerPrefs.GetString("PlayerId", "");
            if (string.IsNullOrEmpty(playerId))
            {
                playerId = System.Guid.NewGuid().ToString();
                PlayerPrefs.SetString("PlayerId", playerId);
            }

            var user = new UserInfo
            {
                userId = playerId,
                userName = playerName,
                avatarIndex = selectedAvatarIndex
            };
            string json = JsonUtility.ToJson(user);

            using (var www = new UnityWebRequest($"{SERVER_URL}/{lobbyId}/join", "POST"))
            {
                www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = 10;

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    RelayLobbyInfo lobby = JsonUtility.FromJson<RelayLobbyInfo>(www.downloadHandler.text);
                    currentLobbyId = lobbyId;
                    localUserName = playerName;

                    yield return StartCoroutine(JoinRelayLobbyCoroutine(lobby.relayJoinCode));

                    if (!networkManager.IsHost)
                    {
                        if (clientHeartbeatRoutine != null) StopCoroutine(clientHeartbeatRoutine);
                        clientHeartbeatRoutine = StartCoroutine(SendClientHeartbeatRoutine());
                    }
                    ShowLobbyUI(lobby);
                    RefreshLobbyList();
                }
                else
                {
                    SendNotification("Failed to join the lobby. It might be full or no longer available.", 1);
                }
            }
        }
        finally
        {
            isJoiningRoom = false;
            isCreatingRoom = false;
            EnableAllJoinButtons();
            EnableMainPanelButton();
        }
    }
    private IEnumerator JoinRelayLobbyCoroutine(string joinCode)
    {
        
        if (joinByIdPanel != null && joinByIdPanel.activeSelf)
            joinByIdPanel.SetActive(false);

        if (!isJoiningRoom)
            yield break;
        
        SendNotification("Connecting to the lobby...", 2);

        bool joinSuccess = false;
        yield return StartCoroutine(JoinRelayCoroutineWrapper(joinCode, (success) => joinSuccess = success));

        if (joinSuccess)
        {
            bool clientStarted = networkManager.StartClient();
            if (clientStarted)
            {
                mainPanel.SetActive(false);
                yield return new WaitForSeconds(2f);
                UIManager.Instance.CloseNotification();
            }
            else
            {
                isJoiningRoom = false;
                SendNotification("Failed to start client. Please try again.", 1);
                HandleExitLogic();
                ResetUIState();
            }
        }
        else
        {
            isJoiningRoom = false;
            SendNotification("Failed to connect to the lobby. Please check the Lobby ID and try again.", 1);
            HandleExitLogic();
            ResetUIState();
        }
    }
    private IEnumerator JoinRelayCoroutineWrapper(string joinCode, Action<bool> callback)
    {
        bool completed = false;
        bool result = false;

        StartCoroutine(ExecuteAsync(async () =>
        {
            result = await JoinRelayAllocation(joinCode);
            completed = true;
        }));

        yield return new WaitUntil(() => completed);
        callback(result);
    }
    private async Task<bool> JoinRelayAllocation(string joinCode)
    {
        if (!isUnityServicesInitialized) return false;

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");
            transport.SetRelayServerData(relayServerData);
            return true;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Failed to join lobby: {e.Message}");
            return false;
        }
    }
    #endregion
}
