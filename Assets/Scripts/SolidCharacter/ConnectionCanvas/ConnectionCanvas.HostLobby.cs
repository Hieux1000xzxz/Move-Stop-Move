using System.Collections;
using System.Text;
using System.Threading.Tasks;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.Networking;

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
        if (!isUnityServicesInitialized)
        {
            SendNotification("Online services are not ready. Please try again.", 1);
            return;
        }
        isCreatingRoom = true;
        SendNotification("Creating lobby...", 2);

        string hostUserId = PlayerPrefs.GetString("PlayerId", "");
        if (string.IsNullOrEmpty(hostUserId))
        {
            hostUserId = System.Guid.NewGuid().ToString();
            PlayerPrefs.SetString("PlayerId", hostUserId);
            PlayerPrefs.Save();
        }

        string joinCode = await CreateRelayAllocation(6);
        if (string.IsNullOrEmpty(joinCode))
        {
            SendNotification("Failed to create lobby. Please try again.", 1);
            EnableMainPanelButton();
            isCreatingRoom = false;
            return;
        }

        if (!networkManager.StartHost())
        {
            EnableMainPanelButton();
            isCreatingRoom = false;
            SendNotification("Failed to start host. Please try again.", 1);
            return;
        }

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
        var request = new RelayLobbyRegistrationRequest
        {
            lobbyName = lobbyName,
            relayJoinCode = joinCode,
            maxPlayers = 6,
            hostName = hostName,
            AvatarIndex = PlayerPrefs.GetInt("PlayerAvatar", 0),
            HostUserId = hostUserId
        };

        bool serverAvailable = false;
        yield return StartCoroutine(CheckServerAvailabilityCoroutine((result) => serverAvailable = result));

        if (serverAvailable)
        {
            yield return StartCoroutine(RegisterRelayLobbyOnServer(request));
        }
    }

    private IEnumerator CheckServerAvailabilityCoroutine(System.Action<bool> callback)
    {
        using (var www = UnityWebRequest.Get($"{SERVER_URL}/ping"))
        {
            www.timeout = 10;
            yield return www.SendWebRequest();
            callback(www.result == UnityWebRequest.Result.Success);
        }
    }

    private IEnumerator RegisterRelayLobbyOnServer(RelayLobbyRegistrationRequest request)
    {
        using (var www = CreatePostRequest($"{SERVER_URL}/register", request))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var lobby = JsonUtility.FromJson<RelayLobbyInfo>(www.downloadHandler.text);
                currentLobbyId = lobby.lobbyId;

                SendNotification("Lobby created successfully!", 4);
                ShowLobbyUI(lobby);
                mainPanel.SetActive(false);
                
                heartbeatRoutine = StartCoroutine(SendHeartbeatRoutine());
                pollLobbyRoutine = StartCoroutine(PollLobbyInfo());
            }
            else
            {
                SendNotification("Failed to create lobby. Please try again.", 1);
                Debug.LogWarning("Could not reach lobby server");

                if (networkManager.IsHost)
                {
                    networkManager.Shutdown();
                }
            }
            isCreatingRoom = false;
        }
    }
    private async Task<string> CreateRelayAllocation(int maxConnections = 5)
    {
        if (!isUnityServicesInitialized) return null;

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
            transport.SetRelayServerData(relayServerData);
            currentRelayJoinCode = joinCode;

            UIManager.Instance.CloseNotification();
            return joinCode;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Failed to create lobby: {e.Message}");
            return null;
        }
    }
    #endregion
}
