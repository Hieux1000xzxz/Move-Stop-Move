using System.Collections;
using System.Text;
using System.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.Networking;
using System;

public partial class ConnectionCanvas
{
    #region Host
    private void OnStartHostClicked()
    {
        if (isCreatingRoom || isJoiningRoom) return;
        DisableMainPanelButton();
        ResetNetworkManager();
        StartHost();
    }

   private async void StartHost()
    {
        if (!CheckUnityServicesReady()) return;

        PrepareForLobbyCreation();

        string hostUserId = GetOrCreatePlayerId();
        string joinCode = await TryCreateRelayJoinCode();

        if (string.IsNullOrEmpty(joinCode))
        {
            HandleRelayFailure();
            return;
        }

        if (!TryStartHostNetwork())
        {
            HandleHostStartFailure();
            return;
        }

        BeginHostLobbyRegistration(hostUserId, joinCode);
    }

    private bool CheckUnityServicesReady()
    {
        if (isUnityServicesInitialized) return true;

        SendNotification("Online services are not ready. Please try again.", 1);
        return false;
    }

    private void PrepareForLobbyCreation()
    {
        isCreatingRoom = true;
        SendNotification("Creating lobby...", 2);
        DisableMainPanelButton();
    }

    private async Task<string> TryCreateRelayJoinCode()
    {
        return await CreateRelayAllocation(6);
    }

    private void HandleRelayFailure()
    {
        SendNotification("Failed to create lobby. Please try again.", 1);
        EnableMainPanelButton();
        isCreatingRoom = false;
    }

    private bool TryStartHostNetwork()
    {
        return networkManager != null && networkManager.StartHost();
    }

    private void HandleHostStartFailure()
    {
        EnableMainPanelButton();
        isCreatingRoom = false;
        SendNotification("Failed to start host. Please try again.", 1);
    }
    private void BeginHostLobbyRegistration(string hostUserId, string joinCode)
    {
        string lobbyName = GenerateRandomLobbyName();
        StartCoroutine(StartHostRoutineCoroutine(localUserName, lobbyName, joinCode, hostUserId));
    }

    private string GenerateRandomLobbyName()
    {
        System.Random random = new System.Random();
        int randomNumber = random.Next(1000, 10000);
        return $"Lobby {randomNumber}";
    }
    private IEnumerator StartHostRoutineCoroutine(string hostName, string lobbyName, string joinCode, string hostUserId)
    {
        var request = BuildRelayLobbyRequest(hostName, lobbyName, joinCode, hostUserId);

        yield return VerifyServerAndRegisterLobby(request);
    }
    private RelayLobbyRegistrationRequest BuildRelayLobbyRequest(string hostName, string lobbyName, string joinCode, string hostUserId)
    {
        return new RelayLobbyRegistrationRequest
        {
            lobbyName = lobbyName,
            relayJoinCode = joinCode,
            maxPlayers = 6,
            hostName = hostName,
            AvatarIndex = LoadAvatarIndex(),
            HostUserId = hostUserId
        };
    }
    private IEnumerator VerifyServerAndRegisterLobby(RelayLobbyRegistrationRequest request)
    {
        bool serverAvailable = false;
        yield return EnsureServerAvailable(result => serverAvailable = result);

        if (!serverAvailable)
        {
            HandleServerUnavailable();
            yield break;
        }

        yield return RegisterRelayLobbyOnServer(request);
    }
    private void HandleServerUnavailable()
    {
        SendNotification("Server unavailable. Please try again later.", 1);
        EnableMainPanelButton();
        isCreatingRoom = false;
    }
    private IEnumerator RegisterRelayLobbyOnServer(RelayLobbyRegistrationRequest request)
    {
        using (var www = CreatePostRequest($"{SERVER_URL}/register", request))
        {
            yield return www.SendWebRequest();

            HandleLobbyRegisterResponse(www);
        }
        isCreatingRoom = false;
    }
    private void HandleLobbyRegisterResponse(UnityWebRequest www)
    {
        if (www.result == UnityWebRequest.Result.Success)
            OnLobbyRegisterSuccess(www.downloadHandler.text);
        else
            OnLobbyRegisterFail(www.error);
    }
    private void OnLobbyRegisterSuccess(string json)
    {
        var lobby = JsonUtility.FromJson<RelayLobbyInfo>(json);
        currentLobbyId = lobby.lobbyId;

        SendNotification("Lobby created successfully!", 4);
        ShowLobbyUI(lobby);
        mainPanel.SetActive(false);

        StartLobbyBackgroundRoutines();
    }
    private void StartLobbyBackgroundRoutines()
    {
        if (heartbeatRoutine != null)
            StopCoroutine(heartbeatRoutine);
        if (pollLobbyRoutine != null)
            StopCoroutine(pollLobbyRoutine);

        heartbeatRoutine = StartCoroutine(SendHeartbeatRoutine());
        pollLobbyRoutine = StartCoroutine(PollLobbyInfo());
    }
    private void OnLobbyRegisterFail(string error)
    {
        SendNotification("Failed to create lobby. Please try again.", 1);

        if (networkManager != null && networkManager.IsHost)
            networkManager.Shutdown();
    }
    private async Task<string> CreateRelayAllocation(int maxConnections = 5)
    {
        if (!isUnityServicesInitialized) return null;

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            SetupRelayTransport(allocation, joinCode);

            UIManager.Instance.CloseNotification();
            return joinCode;
        }
        catch (RelayServiceException e)
        {
            return null;
        }
    }
    private void SetupRelayTransport(Allocation allocation, string joinCode)
    {
        RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
        transport.SetRelayServerData(relayServerData);
        currentRelayJoinCode = joinCode;
    }
    private IEnumerator GetRequest(string endpoint, Action<string> onSuccess, Action<string> onFail = null)
    {
        using (var www = UnityWebRequest.Get($"{SERVER_URL}/{endpoint}"))
        {
            www.timeout = 10;
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
                onSuccess?.Invoke(www.downloadHandler.text);
            else
                onFail?.Invoke(www.error);
        }
    }

    #endregion
}
