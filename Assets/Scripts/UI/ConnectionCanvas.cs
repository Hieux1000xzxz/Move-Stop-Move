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
        StopAllCoroutines();
        if (clientHeartbeatRoutine != null)
        {
            clientHeartbeatRoutine = null;
        }

        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnApplicationQuit()
    {
        Debug.Log("Application quitting - cleaning up network...");

        try
        {
            if (heartbeatRoutine != null)
            {
                StopCoroutine(heartbeatRoutine);
                heartbeatRoutine = null;
            }

            if (pollLobbyRoutine != null)
            {
                StopCoroutine(pollLobbyRoutine);
                pollLobbyRoutine = null;
            }

            if (clientHeartbeatRoutine != null)
            {
                clientHeartbeatRoutine = null;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (NetworkManager.Singleton.IsHost)
                {
                    if (!string.IsNullOrEmpty(currentLobbyId))
                    {
                        SendDeleteLobbySync(currentLobbyId);
                    }
                }
                else if (NetworkManager.Singleton.IsClient)
                {
                    if (!string.IsNullOrEmpty(currentLobbyId) && !string.IsNullOrEmpty(localUserName))
                    {
                        //string playerId = PlayerPrefs.GetString("PlayerId", "");
                        string playerId = GetOrCreatePlayerId();

                        if (!string.IsNullOrEmpty(playerId))
                        {
                            var user = new UserInfo { userId = playerId, userName = localUserName };
                            SendLeaveLobbySync(currentLobbyId, user);
                        }
                    }
                }

                NetworkManager.Singleton.Shutdown();
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.StopPowerupSpawning();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Error during OnApplicationQuit cleanup: {e.Message}");
        }
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