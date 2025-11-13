using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("AI Settings")] [SerializeField]
    private int totalAIQuota = 100;

    [SerializeField] private int currentAIQuota;
    [SerializeField] private AISpawner aiSpawner;
    [SerializeField] private CinemachineZoomController zoomController;
    [SerializeField] private EnemyIndicatorManager enemyIndicatorManager;
    [SerializeField] private GamePlayCanvas gamePlayCanvas;
    [SerializeField] private InteractionCanvas interactionCanvas;
    [SerializeField] private MainMenuCanvas mainMenuCanvas;
    [SerializeField] private ShopCanvas shopCanvas;

    [Header("Camera")] [SerializeField] private CinemachineCamera mainCamera;

    [Header("Powerup")] [SerializeField] private GameObject speedPrefab;
    [SerializeField] private GameObject weaponGrowPrefab;
    [SerializeField] private Transform[] spawnPoints;

    [Header("UI / Preview")] [SerializeField]
    private List<PlayerPreview> playerPreviews = new List<PlayerPreview>();

    public List<PlayerPreview> PlayerPreviews => playerPreviews;


    [Header("Cache")] [SerializeField] private List<NetworkObject> activeAINetworkObjects = new List<NetworkObject>();
    [SerializeField] private List<NetworkObject> activePlayerNetworkObjects = new List<NetworkObject>();
    [SerializeField] private List<NetworkObject> activeEntities = new List<NetworkObject>();

    private Dictionary<NetworkObject, KillScoreDisplay>
        killScoreMap = new Dictionary<NetworkObject, KillScoreDisplay>();

    private Coroutine powerupRoutine;
    public FloatingJoystick mainJoystick;
    private CharacterBase currentSpectatedCharacter;


    private int spectatorIndex = 0;

    public NetworkVariable<int> ActiveAICount = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> ActivePlayerCount = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> RemainingAIQuota = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> EnemyCount = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private int totalSpawned = 0;
    private bool isGameStarted = false;
    [Header("Spectator")] public ulong CurrentSpectatedId = 0;
    public bool IsSpectatorMode = false;
    public bool IsGameStarted => isGameStarted;

    protected void Awake()
    {
        //PlayerPrefs.DeleteAll();
        //PlayerPrefs.Save();
        Instance = this;
        isGameStarted = false;
        currentAIQuota = totalAIQuota;
        shopCanvas.LoadSelectedWeapon();
        DisableGamePlaySystem();
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        Input.multiTouchEnabled = false;
        LookScreen();
    }

    private void LookScreen()
    {
        Screen.orientation = ScreenOrientation.Portrait;

        Screen.autorotateToLandscapeLeft = false;
        Screen.autorotateToLandscapeRight = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToPortrait = true;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsClient)
        {
            //ActiveAICount.OnValueChanged += OnAICountChanged;
            //ActivePlayerCount.OnValueChanged += OnPlayerCountChanged;
            //RemainingAIQuota.OnValueChanged += OnQuotaChanged;
            EnemyCount.OnValueChanged += OnEnemyCountChanged;
            UIManager.Instance?.UpdateEnemyCount(EnemyCount.Value);
        }
    }

    private new void OnDestroy()
    {
        if (IsClient)
        {
            //ActiveAICount.OnValueChanged -= OnAICountChanged;
            //ActivePlayerCount.OnValueChanged -= OnPlayerCountChanged;
            //RemainingAIQuota.OnValueChanged -= OnQuotaChanged;
            EnemyCount.OnValueChanged -= OnEnemyCountChanged;
        }
    }

    private void OnEnemyCountChanged(int previous, int current)
    {
        UIManager.Instance?.UpdateEnemyCount(current);
    }

    public bool CanSpawnAI()
    {
        return currentAIQuota - activeAINetworkObjects.Count > 0;
    }

    private void UpdateEnemyCount()
    {
        int enemyLeft = RemainingAIQuota.Value + ActivePlayerCount.Value;
        if (enemyLeft < 0) enemyLeft = 0;

        EnemyCount.Value = enemyLeft;
    }

    public bool TryRegisterAI(NetworkObject aiNetworkObject)
    {
        if (!IsServer || aiNetworkObject == null) return false;

        if (activeAINetworkObjects.Contains(aiNetworkObject))
            return false;

        activeAINetworkObjects.Add(aiNetworkObject);
        ActiveAICount.Value = activeAINetworkObjects.Count;

        AddToActiveEntities(aiNetworkObject);

        totalSpawned++;
        return true;
    }

    private void AddToActiveEntities(NetworkObject networkObject)
    {
        if (!activeEntities.Contains(networkObject))
        {
            activeEntities.Add(networkObject);
        }
    }

    public void CaculateTotalQuota()
    {
        if (ActivePlayerCount.Value == 0)
        {
            totalAIQuota = totalAIQuota - 1;
        }
        else
        {
            totalAIQuota = totalAIQuota - ActivePlayerCount.Value;
        }
    }

    public void UnregisterAI(NetworkObject aiNetworkObject)
    {
        if (!IsServer || aiNetworkObject == null) return;

        if (activeAINetworkObjects.Remove(aiNetworkObject))
        {
            currentAIQuota--;
            ActiveAICount.Value = activeAINetworkObjects.Count;
            RemainingAIQuota.Value = currentAIQuota;
            RemoveFromActiveEntities(aiNetworkObject);
            UpdateEnemyCount();
        }

        Invoke(nameof(CheckLastSurvivor), 1f);
    }

    private void RemoveFromActiveEntities(NetworkObject networkObject)
    {
        activeEntities.Remove(networkObject);
        UnregisterKillScore(networkObject);
    }

    public void RegisterPlayerInGame(NetworkObject playerNetworkObject)
    {
        if (playerNetworkObject == null) return;

        activePlayerNetworkObjects.Add(playerNetworkObject);
        ActivePlayerCount.Value = activePlayerNetworkObjects.Count;

        AddToActiveEntities(playerNetworkObject);

        UpdateEnemyCount();
    }

    public void UnregisterPlayerInGame(NetworkObject playerNetworkObject)
    {
        if (playerNetworkObject == null) return;

        if (activePlayerNetworkObjects.Remove(playerNetworkObject))
        {
            ActivePlayerCount.Value = activePlayerNetworkObjects.Count;
            RemoveFromActiveEntities(playerNetworkObject);
            UpdateEnemyCount();
            Invoke(nameof(CheckLastSurvivor), 1.5f);
        }
    }

    public void RegisterKillScore(NetworkObject netObj, KillScoreDisplay killScore)
    {
        if (netObj != null && killScore != null && !killScoreMap.ContainsKey(netObj))
        {
            killScoreMap[netObj] = killScore;
        }
    }

    public void UnregisterKillScore(NetworkObject netObj)
    {
        if (netObj != null)
        {
            killScoreMap.Remove(netObj);
        }
    }

    private void CheckLastSurvivor()
    {
        if (!ShouldCheckLastSurvivor()) return;

        if (!IsOnlyOneSurvivor()) return;

        HandleLastSurvivor();
    }

    private bool ShouldCheckLastSurvivor()
    {
        return IsServer && isGameStarted && EnemyCount.Value <= 1 && totalSpawned >= 2;
    }

    private bool IsOnlyOneSurvivor()
    {
        return activeEntities.Count == 1;
    }

    private void HandleLastSurvivor()
    {
        var lastNetObj = GetLastSurvivor();
        if (lastNetObj == null) return;

        FocusCameraOnSurvivor(lastNetObj);

        if (HasActivePlayers())
        {
            HandleMultiplayerWin(lastNetObj);
        }
        else
        {
            HandleSinglePlayerWin();
        }
    }

    private NetworkObject GetLastSurvivor()
    {
        return activeEntities[0];
    }

    private void FocusCameraOnSurvivor(NetworkObject survivor)
    {
        FocusCameraOnTargetClientRpc(survivor);
    }

    private bool HasActivePlayers()
    {
        return ActivePlayerCount.Value > 0;
    }

    private void HandleMultiplayerWin(NetworkObject winner)
    {
        GameWinClientRpc();
        NotifyWinner(winner);
    }

    private void NotifyWinner(NetworkObject winner)
    {
        var winnerId = winner.OwnerClientId;
        var clientRpcParams = CreateTargetClientRpcParams(winnerId);

        NotifyClientCommitCoinClientRpc(clientRpcParams);
        WinnerClientRpc(clientRpcParams);
    }

    private ClientRpcParams CreateTargetClientRpcParams(ulong targetClientId)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { targetClientId }
            }
        };
    }

    private void HandleSinglePlayerWin()
    {
        CoinManager.Instance.CommitSessionCoins();
        GameWinClientRpc();
    }

    private void SetupWinCamera()
    {
        UIManager.Instance.HideCountText();
        zoomController.baseFOV = 35f;
        zoomController.baseFollowY = 5f;
    }

    [ClientRpc]
    private void GameWinClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (gamePlayCanvas != null)
        {
            gamePlayCanvas.OnGameWin();
            SetupWinCamera();
        }
    }

    [ClientRpc]
    private void FocusCameraOnTargetClientRpc(NetworkObjectReference targetRef)
    {
        if (targetRef.TryGet(out NetworkObject netObj))
        {
            Transform t = netObj.transform;
            if (t != null)
            {
                BindCameraToPlayer(t);
            }
        }
    }

    public void ResetGame()
    {
        CaculateTotalQuota();
        currentAIQuota = totalAIQuota;
        totalSpawned = 0;

        DeactivateAllAI();

        activeAINetworkObjects.Clear();

        ActiveAICount.Value = 0;
        ActivePlayerCount.Value = activePlayerNetworkObjects.Count;
        RemainingAIQuota.Value = currentAIQuota;
        UpdateEnemyCount();
    }

    private bool IsValidActiveObject(NetworkObject netObj)
    {
        return netObj != null && netObj.gameObject != null;
    }

    private void DeactivateAllAI()
    {
        foreach (var aiNetObj in activeAINetworkObjects)
        {
            if (IsValidActiveObject(aiNetObj))
            {
                aiNetObj.gameObject.SetActive(false);
            }
        }
    }

    public void StartGame()
    {
        isGameStarted = true;
        EnableGamePlaySystem();
        if (IsServer)
        {
            ResetGame();
        }

        HidePlayerPreview();
        UIManager.Instance.CloseAllUI();
    }

    public void GameOver()
    {
        CoinManager.Instance.CommitSessionCoins();
        gamePlayCanvas.OnGameOver();
    }

    private void DisableGamePlaySystem()
    {
        aiSpawner.enabled = false;
        enemyIndicatorManager.enabled = false;
        interactionCanvas.Hide();
    }

    private void EnableGamePlaySystem()
    {
        aiSpawner.enabled = true;
        zoomController.baseFOV = 60f;
        zoomController.baseFollowY = 15f;
        enemyIndicatorManager.enabled = true;
        interactionCanvas.Show();
        gamePlayCanvas.Show();
    }

    public void BindCameraToPlayer(Transform player)
    {
        if (mainCamera != null)
        {
            mainCamera.Follow = player;
            mainCamera.LookAt = player;
        }
    }

    public void BindKillScoreDisplay(KillScoreDisplay killScore)
    {
        zoomController.SetUp(killScore);
    }

    public void BindJoystick(Player player)
    {
        player.SetJoystick(mainJoystick);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestStartGameServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!isGameStarted)
        {
            StartGame();
            StartGameClientRpc();
        }
    }

    [ClientRpc]
    public void StartGameClientRpc(ClientRpcParams rpcParams = default)
    {
        if (!IsServer)
        {
            StartGame();
        }
    }

    public void StartPowerupSpawning()
    {
        if (!IsServer) return;
        if (powerupRoutine == null)
        {
            powerupRoutine = StartCoroutine(SpawnPowerupRoutine());
        }
    }

    public void StopPowerupSpawning()
    {
        if (!IsServer) return;
        if (powerupRoutine != null)
        {
            StopCoroutine(powerupRoutine);
            powerupRoutine = null;
        }
    }

    private IEnumerator SpawnPowerupRoutine()
    {
        yield return new WaitForSeconds(5f);
        while (true)
        {
            SpawnPowerup();
            yield return new WaitForSeconds(12f);
        }
    }

    private List<NetworkObject> GetAliveSpectatorTargets()
    {
        var result = new List<NetworkObject>();
        foreach (var entity in activeEntities)
        {
            if (entity != null && entity.IsSpawned)
                result.Add(entity);
        }

        return result;
    }


    public void EnableSpectatorMode()
    {
        var alive = GetAliveSpectatorTargets();
        if (alive.Count == 0)
        {
            return;
        }

        spectatorIndex = 0;
        FocusCameraOnTarget(alive[spectatorIndex]);
    }

    private void FocusCameraOnTarget(NetworkObject netObj)
    {
        if (netObj == null) return;
        UpdateCameraAndTracking(netObj.transform, netObj);
    }

    private void TrackSpectatedCoin(CharacterBase character)
    {
        if (currentSpectatedCharacter != null)
        {
            currentSpectatedCharacter.SessionCoin.OnValueChanged -= OnSpectatedCoinChanged;
        }

        currentSpectatedCharacter = character;

        CoinManager.Instance?.UpdateSpectatorCoin(character.SessionCoin.Value);

        character.SessionCoin.OnValueChanged += OnSpectatedCoinChanged;
    }

    private void OnSpectatedCoinChanged(int oldVal, int newVal)
    {
        CoinManager.Instance?.UpdateSpectatorCoin(newVal);
    }

    private void SpawnPowerup()
    {
        if (!IsServer) return;
        if (spawnPoints.Length == 0) return;
        int index = Random.Range(0, spawnPoints.Length);
        Transform spawnPoint = spawnPoints[index];
        PowerupType type = (Random.value > 0.5f) ? PowerupType.SpeedBoost : PowerupType.WeaponGrow;
        GameObject obj = ObjectPool.Instance.SpawnPowerup(type, spawnPoint.position, Quaternion.identity);
        if (obj == null) return;
        obj.GetComponent<Powerup>().SetType(type);
    }

    private void SetPlayerPreviewsActive(bool isActive)
    {
        foreach (PlayerPreview preview in playerPreviews)
        {
            if (preview != null)
            {
                preview.gameObject.SetActive(isActive);
            }
        }
    }

    public void HidePlayerPreview()
    {
        SetPlayerPreviewsActive(false);
    }

    public void ShowPlayerPreview()
    {
        SetPlayerPreviewsActive(true);
        zoomController.SetUpBaseZoom();
    }

    private void ChangeSpectatorTarget(int direction, ulong clientId)
    {
        if (activeEntities.Count <= 1) return;

        // Update index
        if (direction > 0)
        {
            spectatorIndex = (spectatorIndex + 1) % activeEntities.Count;
        }
        else
        {
            spectatorIndex--;
            if (spectatorIndex < 0) spectatorIndex = activeEntities.Count - 1;
        }

        NetworkObject netObj = activeEntities[spectatorIndex];
        if (netObj == null) return;

        var clientRpcParams = CreateTargetClientRpcParams(clientId);
        FocusCameraOnTargetClientRpc(netObj, clientRpcParams);
    }

    [ClientRpc]
    public void GameOverTargetClientRpc(ClientRpcParams clientRpcParams = default)
    {
        GameOver();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestNextSpectatorTargetServerRpc(ServerRpcParams rpcParams = default)
    {
        ChangeSpectatorTarget(1, rpcParams.Receive.SenderClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestPreviousSpectatorTargetServerRpc(ServerRpcParams rpcParams = default)
    {
        ChangeSpectatorTarget(-1, rpcParams.Receive.SenderClientId);
    }

    [ClientRpc]
    private void FocusCameraOnTargetClientRpc(NetworkObjectReference targetRef, ClientRpcParams rpcParams = default)
    {
        if (rpcParams.Send.TargetClientIds != null &&
            rpcParams.Send.TargetClientIds.Count > 0 &&
            NetworkManager.Singleton.LocalClientId != rpcParams.Send.TargetClientIds[0])
            return;

        if (targetRef.TryGet(out NetworkObject netObj))
        {
            UpdateCameraAndTracking(netObj.transform, netObj);
        }
    }

    private void UpdateCameraAndTracking(Transform target, NetworkObject netObj)
    {
        if (mainCamera != null && target != null)
        {
            mainCamera.Follow = target;
            mainCamera.LookAt = target;
        }

        if (killScoreMap.TryGetValue(netObj, out var killScore))
        {
            zoomController.SetUp(killScore);
        }

        if (netObj.TryGetComponent(out CharacterBase character))
        {
            TrackSpectatedCoin(character);
            CurrentSpectatedId = netObj.OwnerClientId;
        }
    }

    [ClientRpc]
    private void WinnerClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (gamePlayCanvas != null)
        {
            gamePlayCanvas.OnWinner();
            SetupWinCamera();
        }
    }

    #region Death Handling

    public void HandleCharacterDeath(CharacterBase character)
    {
        if (character == null) return;

        StartCoroutine(DelayedWeaponCleanup(character, 0.2f));
    }

    private IEnumerator DelayedWeaponCleanup(CharacterBase character, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (character == null || ObjectPool.Instance == null)
            yield break;

        WeaponBase weapon = character.currentWeaponPublic;
        if (weapon != null && weapon.NetworkObj != null && weapon.NetworkObj.IsSpawned)
        {
            ObjectPool.Instance.ReleaseWeapon(weapon.gameObject);
        }
    }

    #endregion

    [ClientRpc]
    private void NotifyClientCommitCoinClientRpc(ClientRpcParams clientRpcParams = default)
    {
        CoinManager.Instance.CommitSessionCoins();
    }
}