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

        SetJoinState(true);
        joinLobbyCoroutine = StartCoroutine(JoinLobbyRoutine(lobbyId, localUserName));
    }

    public void JoinLobbyDirect(RelayLobbyInfo lobby)
    {
        if (isJoiningRoom || isCreatingRoom) return;
        if (lobby == null) return;

        SetJoinState(true);
        ResetNetworkManager();

        joinLobbyCoroutine = StartCoroutine(JoinLobbyRoutine(lobby.lobbyId, localUserName));
    }

    private IEnumerator JoinLobbyRoutine(string lobbyId, string playerName)
    {
        try
        {
            bool validLobby = false;
            yield return CheckLobbyAvailability(lobbyId, result => validLobby = result);
            if (!validLobby) yield break;

            yield return ProcessLobbyJoin(lobbyId, playerName);
        }
        finally
        {
            SetJoinState(false);
        }
    }

    private void HandleJoinFail(string message)
    {
        SetJoinState(false);
        SendNotification(message, 1);
        HandleExitLogic();
    }

    private IEnumerator JoinRelayAndSetupClient(RelayLobbyInfo lobby)
    {
        yield return ConnectToRelay(lobby.relayJoinCode);
        yield return SetupClientAfterRelay(lobby);
    }

    private IEnumerator ConnectToRelay(string joinCode)
    {
        PrepareJoinRelayUI();
        SendNotification("Connecting to the lobby...", 2);

        bool success = false;
        yield return ExecuteAsync(async () => success = await JoinRelayAllocation(joinCode));

        if (!success)
        {
            HandleJoinFail("Failed to connect to the lobby. Please check the Lobby ID and try again.");
            yield break;
        }
    }

    private IEnumerator SetupClientAfterRelay(RelayLobbyInfo lobby)
    {
        bool clientStarted = networkManager.StartClient();
        if (!clientStarted)
        {
            HandleJoinFail("Failed to start client. Please try again.");
            yield break;
        }

        mainPanel.SetActive(false);
        StartCoroutine(CloseNotificationAfterDelay(2f));

        if (clientHeartbeatRoutine != null)
            StopCoroutine(clientHeartbeatRoutine);

        clientHeartbeatRoutine = StartCoroutine(SendClientHeartbeatRoutine());

        ShowLobbyUI(lobby);
        SafeRefreshLobby();
    }

    private IEnumerator ProcessLobbyJoin(string lobbyId, string playerName)
    {
        string playerId = GetOrCreatePlayerId();

        var user = new UserInfo
        {
            userId = playerId,
            userName = playerName,
            avatarIndex = selectedAvatarIndex
        };

        using (var www = CreatePostRequest($"{SERVER_URL}/{lobbyId}/join", user))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                SendNotification("Failed to join the lobby. It might be full or no longer available.", 1);
                yield break;
            }

            RelayLobbyInfo lobby = JsonUtility.FromJson<RelayLobbyInfo>(www.downloadHandler.text);
            currentLobbyId = lobbyId;
            localUserName = playerName;

            yield return JoinRelayAndSetupClient(lobby);
        }
    }

    private IEnumerator CheckLobbyAvailability(string lobbyId, Action<bool> callback)
    {
        using (var checkWww = UnityWebRequest.Get($"{SERVER_URL}/find/{lobbyId}"))
        {
            checkWww.timeout = 10;
            yield return checkWww.SendWebRequest();

            if (checkWww.result != UnityWebRequest.Result.Success)
            {
                SendNotification("Failed to join lobby. Please try again.", 1);
                //HandleExitLogic();
                callback(false);
                yield break;
            }

            RelayLobbyInfo lobbyCheck = JsonUtility.FromJson<RelayLobbyInfo>(checkWww.downloadHandler.text);
            if (lobbyCheck == null || lobbyCheck.isGameStarted)
            {
                SendNotification("Lobby not found or game already started.", 1);
                callback(false);
                yield break;
            }

            callback(true);
        }
    }

    private void SetJoinState(bool active)
    {
        isJoiningRoom = active;
        isCreatingRoom = false;

        if (active)
        {
            DisableMainPanelButton();
            DisableAllJoinButtons();
            UIManager.Instance.CloseNotification();
        }
        else
        {
            EnableMainPanelButton();
            EnableAllJoinButtons();
        }
    }

    private void PrepareJoinRelayUI()
    {
        if (joinByIdPanel != null && joinByIdPanel.activeSelf)
            joinByIdPanel.SetActive(false);
    }

    private IEnumerator CloseNotificationAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        UIManager.Instance.CloseNotification();
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