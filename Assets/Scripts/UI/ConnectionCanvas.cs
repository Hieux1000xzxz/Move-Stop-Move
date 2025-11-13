using System;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

public partial class ConnectionCanvas : BaseCanvas
{
    [Header("UI Panels")] [SerializeField] private GameObject modePanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject playerInfoPanel;
    [SerializeField] private GamePlayCanvas gameplayCanvas;

    [Header("Mode Panel")] [SerializeField]
    private Button singleButton;

    [SerializeField] private Button onlineButton;
    [SerializeField] private Button backToMenuButton;

    [Header("Main Panel")] [SerializeField]
    private Button startHostButton;

    [SerializeField] private Button backButton;
    [SerializeField] private Button joinByIdButton;
    [SerializeField] private Button editProfileButton;
    [SerializeField] private Transform lobbyListContainer;
    [SerializeField] private LobbyItem lobbyItemPrefab;

    [Header("Lobby Panel")] [SerializeField]
    private Transform playerListContainer;

    [SerializeField] private TextMeshProUGUI lobbyId;
    [SerializeField] private PlayerItem playerItemPrefab;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button settingButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button editProfileInLobbyButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private Sprite readySprite;
    [SerializeField] private Sprite unReadySprite;

    [Header("Join by ID Panel")] [SerializeField]
    private GameObject joinByIdPanel;

    [SerializeField] private TMP_InputField lobbyIdInputField;
    [SerializeField] private Button confirmJoinButton;
    [SerializeField] private Button cancelJoinButton;

    [Header("Player Info Panel")] [SerializeField]
    private TMP_InputField playerNameInputField;

    [SerializeField] private Transform avatarSelectionContainer;
    [SerializeField] private AvatarItem avatarItemPrefab;
    [SerializeField] private Button confirmPlayerInfoButton;
    [SerializeField] private Button cancelPlayerInfoButton;
    [SerializeField] private TextMeshProUGUI playerInfoTitle;

    [Header("Setting Panel")] [SerializeField]
    private GameObject settingPanel;

    [SerializeField] private TMP_InputField roomNameInputField;
    [SerializeField] private Button confirmRoomNameButton;
    [SerializeField] private Button closeSettingButton;

    [Header("Network")] [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport transport;

    [SerializeField] private GameObject playerPreview;
    [SerializeField] private CinemachineCamera mainCamera;
    [SerializeField] private Sprite[] availableAvatars;

    [Header("Cache Lists")] [SerializeField]
    private List<AvatarItem> cachedAvatarItems = new List<AvatarItem>();

    [SerializeField] private List<PlayerItem> cachedPlayerItems = new List<PlayerItem>();
    [SerializeField] private List<LobbyItem> cachedLobbyItems = new List<LobbyItem>();

    [Header("Status Flags")] private bool isCreatingRoom = false;
    private bool isJoiningRoom = false;

    private const string SERVER_URL = "https://mini-server-v6.onrender.com/api/lobby";
    private const string DEFAULT_LOBBY_NAME = "Lobby";
    private const string DEFAULT_PLAYER_NAME = "Player";

    private int selectedAvatarIndex = 0;
    private string currentLobbyId = string.Empty;
    private string localUserName = DEFAULT_PLAYER_NAME;
    private string currentRelayJoinCode = string.Empty;

    private Coroutine heartbeatRoutine;
    private Coroutine pollLobbyRoutine;
    private Coroutine autoRefreshLobbyRoutine;
    private RelayLobbyInfo pendingLobbyJoin;
    public bool isSinglePlayerMode = false;
    private bool isReady = false;
    private RelayLobbyInfo currentLobbyInfo;
    private bool isUnityServicesInitialized = false;
    private bool canToggleReady = true;
    private Coroutine clientHeartbeatRoutine;
    private bool isIntentionalDisconnect = false;
    private Coroutine joinLobbyCoroutine;

    #region Unity Lifecycle

    private async void Start()
    {
        InitializeButtons();
        InitializeNetworkCallbacks();
        SetUIState(true);

        await InitializeUnityServices();
        UpdateReadyButtonStatus();
        RefreshLobbyList();
        InitializeAvatarSelection();
        InitializePlayerProfile();
        InitializeInputValidation();
        autoRefreshLobbyRoutine = StartCoroutine(AutoRefreshLobbyList());
    }

    private void OnDestroy()
    {
        StopAllRunningCoroutines();
        CleanupNetworkCallbacks();
    }

    private void OnApplicationQuit()
    {
        Debug.Log("[QUIT] Application quitting - cleaning up...");

        try
        {
            StopAllRunningCoroutines();
            CleanupNetworkCallbacks();
            HandleNetworkQuit();
            StopPowerupSystem();

            Debug.Log("[QUIT] Cleanup completed successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[QUIT] Error during cleanup: {e.Message}");
        }
    }

    #endregion

    #region Coroutine Management

    private void StopAllRunningCoroutines()
    {
        StopAllCoroutines();

        // Reset all coroutine references
        heartbeatRoutine = null;
        pollLobbyRoutine = null;
        clientHeartbeatRoutine = null;
        autoRefreshLobbyRoutine = null;
        joinLobbyCoroutine = null;
    }

    #endregion

    #region Network Cleanup

    private void CleanupNetworkCallbacks()
    {
        if (networkManager == null) return;

        networkManager.OnClientConnectedCallback -= OnClientConnected;
        networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void HandleNetworkQuit()
    {
        if (!IsNetworkActive()) return;

        if (IsHost())
        {
            HandleHostQuit();
        }
        else
        {
            HandleClientQuit();
        }

        ShutdownNetworkManager();
    }

    private void HandleHostQuit()
    {
        if (string.IsNullOrEmpty(currentLobbyId)) return;

        SendDeleteLobbySync(currentLobbyId);
        Debug.Log($"[QUIT] Host deleted lobby: {currentLobbyId}");
    }

    private void HandleClientQuit()
    {
        if (string.IsNullOrEmpty(currentLobbyId)) return;

        string playerId = GetOrCreatePlayerId();
        if (string.IsNullOrEmpty(playerId)) return;

        var user = new UserInfo
        {
            userId = playerId,
            userName = localUserName
        };

        SendLeaveLobbySync(currentLobbyId, user);
        Debug.Log($"[QUIT] Client left lobby: {currentLobbyId}");
    }

    private void ShutdownNetworkManager()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("[QUIT] Network shutdown completed.");
        }
    }

    #endregion

    #region Game Systems Cleanup

    private void StopPowerupSystem()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StopPowerupSpawning();
            Debug.Log("[QUIT] Powerup system stopped.");
        }
    }

    #endregion

    #region Helper Methods

    private bool IsNetworkActive()
    {
        return NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsListening;
    }

    private bool IsHost()
    {
        return NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsHost;
    }

    #endregion
}

#region UpdateRequest

[Serializable]
public class UpdateReadyRequest
{
    public string UserId;
    public bool IsReady;
}

[Serializable]
public class UpdateLobbyNameRequest
{
    public string lobbyId;
    public string lobbyName;
    public string requestingUserId;
}

[Serializable]
public class ClientHeartbeatRequest
{
    public string UserId;
}

#endregion

[Serializable]
public class RelayLobbyInfo
{
    public string lobbyId;
    public string lobbyName;
    public string relayJoinCode;
    public int currentPlayers;
    public int maxPlayers;
    public string createdAt;
    public string hostUserId;
    public bool isGameStarted;
    public List<UserInfo> users = new List<UserInfo>();
}

[Serializable]
public class RelayLobbyRegistrationRequest
{
    public string lobbyName;
    public string relayJoinCode;
    public int maxPlayers;
    public string hostName;
    public int AvatarIndex;
    public string HostUserId;
}

[Serializable]
public class UserInfo
{
    public string userId;
    public string userName;
    public int avatarIndex;
    public ulong clientId;
    public bool isReady;
}

public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        string newJson = "{ \"array\": " + json + "}";
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
        return wrapper.array;
    }

    [Serializable]
    private class Wrapper<T>
    {
#pragma warning disable 0649
        public T[] array;
#pragma warning restore 0649
    }
}