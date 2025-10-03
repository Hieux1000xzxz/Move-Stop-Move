using System;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using System.Collections.Generic;
using Unity.Networking.Transport.Relay;

public class RelayManager : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport transport;

    private string currentJoinCode;
    private bool isInitialized = false;

    public static RelayManager Instance { get; private set; }

    public event System.Action<string> OnRelayJoinCodeGenerated;
    public event System.Action<bool> OnRelayConnectionResult;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private async void Start()
    {
        await InitializeUnityServices();
    }

    private async Task InitializeUnityServices()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            isInitialized = true;
            Debug.Log($"Unity Services initialized. Player ID: {AuthenticationService.Instance.PlayerId}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize Unity Services: {e.Message}");
            isInitialized = false;
        }
    }

    public async Task<string> CreateRelayAllocation(int maxConnections = 5)
    {
        if (!isInitialized)
        {
            Debug.LogError("Unity Services not initialized");
            return null;
        }

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

            currentJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            RelayServerData relayServerData = new RelayServerData(allocation, "dtls");
            transport.SetRelayServerData(relayServerData);

            Debug.Log($"Relay allocation created. Join Code: {currentJoinCode}");
            OnRelayJoinCodeGenerated?.Invoke(currentJoinCode);

            return currentJoinCode;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Failed to create relay allocation: {e.Message}");
            return null;
        }
    }

    public async Task<bool> JoinRelayAllocation(string joinCode)
    {
        if (!isInitialized)
        {
            Debug.LogError("Unity Services not initialized");
            return false;
        }

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            RelayServerData relayServerData = new RelayServerData(joinAllocation, "dtls");
            transport.SetRelayServerData(relayServerData);

            Debug.Log($"Successfully joined relay with code: {joinCode}");
            OnRelayConnectionResult?.Invoke(true);
            return true;
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"Failed to join relay: {e.Message}");
            OnRelayConnectionResult?.Invoke(false);
            return false;
        }
    }

    public async Task<bool> StartHostWithRelay(int maxConnections = 5)
    {
        string joinCode = await CreateRelayAllocation(maxConnections);
        if (string.IsNullOrEmpty(joinCode))
        {
            return false;
        }

        // Start as host
        bool hostStarted = networkManager.StartHost();
        if (!hostStarted)
        {
            Debug.LogError("Failed to start host");
            return false;
        }

        Debug.Log($"Host started with Relay. Join Code: {joinCode}");
        return true;
    }

    public async Task<bool> StartClientWithRelay(string joinCode)
    {
        bool joined = await JoinRelayAllocation(joinCode);
        if (!joined)
        {
            return false;
        }

        // Start as client
        bool clientStarted = networkManager.StartClient();
        if (!clientStarted)
        {
            Debug.LogError("Failed to start client");
            return false;
        }

        Debug.Log($"Client started with Relay using join code: {joinCode}");
        return true;
    }

    public string GetCurrentJoinCode()
    {
        return currentJoinCode;
    }

    public bool IsServiceInitialized()
    {
        return isInitialized;
    }
}