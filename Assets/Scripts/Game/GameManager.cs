using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("AI Settings")]
    [SerializeField] private int totalAIQuota = 100;
    [SerializeField] private int currentAIQuota;
    [SerializeField] private AISpawner aiSpawner;
    [SerializeField] private CinemachineZoomController zoomController;
    [SerializeField] private EnemyIndicatorManager enemyIndicatorManager;
    [SerializeField] private GamePlayCanvas gamePlayCanvas;
    [SerializeField] private InteractionCanvas interactionCanvas;
    [SerializeField] private MainMenuCanvas mainMenuCanvas;
    [SerializeField] private ShopCanvas shopCanvas;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera mainCamera;

    [Header("Powerup")]
    [SerializeField] private GameObject speedPrefab;
    [SerializeField] private GameObject weaponGrowPrefab;
    [SerializeField] private Transform[] spawnPoints;

    [Header("UI / Preview")]
    [SerializeField] private GameObject playerPreview;

    private Coroutine powerupRoutine;
    public FloatingJoystick mainJoystick;

    private List<NetworkObject> activeAINetworkObjects = new List<NetworkObject>();
    private List<NetworkObject> activePlayerNetworkObjects = new List<NetworkObject>();
    private List<NetworkObject> activeEntities = new List<NetworkObject>();
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
    private int totalKilled = 0;
    private bool isGameStarted = false;

    public bool IsGameStarted => isGameStarted;

    protected void Awake()
    {
        Instance = this;
        isGameStarted = false;
        currentAIQuota = totalAIQuota;
        shopCanvas.LoadSelectedWeapon();
        DisableGamePlaySystem();
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
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

    private void OnDestroy()
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
        int enemyLeft = RemainingAIQuota.Value + ActivePlayerCount.Value - 1;
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

        if (!activeEntities.Contains(aiNetworkObject))
        {
            activeEntities.Add(aiNetworkObject);
        }
        totalSpawned++;
        return true;
    }
    public void CaculateTotalQuota()
    {
        totalAIQuota = totalAIQuota - ActivePlayerCount.Value + 1;
    }

    public void UnregisterAI(NetworkObject aiNetworkObject)
    {
        if (!IsServer || aiNetworkObject == null) return;

        if (activeAINetworkObjects.Remove(aiNetworkObject))
        {
            currentAIQuota--;
            ActiveAICount.Value = activeAINetworkObjects.Count;
            RemainingAIQuota.Value = currentAIQuota;
            activeEntities.Remove(aiNetworkObject);
            UpdateEnemyCount();
        }

        CheckLastSurvivor();
    }

    public void RegisterPlayerInGame(NetworkObject playerNetworkObject)
    {
        if (playerNetworkObject == null) return;

        activePlayerNetworkObjects.Add(playerNetworkObject);
        ActivePlayerCount.Value = activePlayerNetworkObjects.Count;

        if (!activeEntities.Contains(playerNetworkObject))
        {
            activeEntities.Add(playerNetworkObject);
        }
        UpdateEnemyCount();
    }

    public void UnregisterPlayerInGame(NetworkObject playerNetworkObject)
    {
        if (playerNetworkObject == null) return;

        if (activePlayerNetworkObjects.Remove(playerNetworkObject))
        {
            ActivePlayerCount.Value = activePlayerNetworkObjects.Count;
            activeEntities.Remove(playerNetworkObject);
            UpdateEnemyCount();
            CheckLastSurvivor();
        }
    }

    private void CheckLastSurvivor()
    {
        if (!IsServer) return;
        if (!isGameStarted) return;

        if (EnemyCount.Value > 0) return;

        if (totalSpawned < 2) return;
        if (activeEntities.Count == 1)
        {
            var lastNetObj = activeEntities[0];
            if (lastNetObj != null)
            {
                FocusCameraOnTargetClientRpc(lastNetObj);

                GameWinClientRpc();

                if (lastNetObj.TryGetComponent(out NetworkObject netObj))
                {
                    var winnerId = netObj.OwnerClientId;
                    var clientRpcParams = new ClientRpcParams
                    {
                        Send = new ClientRpcSendParams { TargetClientIds = new[] { winnerId } }
                    };
                    WinnerClientRpc(clientRpcParams);
                }
            }
        }
    }


    [ClientRpc]
    private void GameWinClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (gamePlayCanvas != null)
        {
            gamePlayCanvas.OnGameWin();
            UIManager.Instance.HideCountText();
            zoomController.baseFOV = 35f;
            zoomController.baseFollowY = 5f;
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
        totalKilled = 0;

        foreach (var aiNetObj in activeAINetworkObjects)
        {
            if (aiNetObj != null && aiNetObj.gameObject != null)
                aiNetObj.gameObject.SetActive(false);
        }
        activeAINetworkObjects.Clear();

        ActiveAICount.Value = 0;
        ActivePlayerCount.Value = activePlayerNetworkObjects.Count;
        RemainingAIQuota.Value = currentAIQuota;
        UpdateEnemyCount();
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
        //DisableGamePlaySystem();
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

    public void ShowMainMenu()
    {
        if (mainMenuCanvas != null)
        {
            mainMenuCanvas.Show();
        }
    }

    public void HideMainMenu()
    {
        if (mainMenuCanvas != null)
        {
            mainMenuCanvas.Hide();
        }
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

    public List<Transform> GetAliveSpectatorTargets()
    {
        List<Transform> targets = new List<Transform>();

        foreach (var playerNetObj in activePlayerNetworkObjects)
        {
            if (playerNetObj != null)
            {
                var health = playerNetObj.GetComponent<Health>();
                if (health != null && !health.IsDead)
                    targets.Add(playerNetObj.transform);
            }
        }

        if (targets.Count == 0)
        {
            foreach (var aiNetObj in activeAINetworkObjects)
            {
                if (aiNetObj != null)
                {
                    var health = aiNetObj.GetComponent<Health>();
                    if (health != null && !health.IsDead)
                        targets.Add(aiNetObj.transform);
                }
            }
        }

        return targets;
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

    public void NextSpectatorTarget()
    {
        var alive = GetAliveSpectatorTargets();
        if (alive.Count == 0)
        {
            return;
        }

        spectatorIndex = (spectatorIndex + 1) % alive.Count;
        FocusCameraOnTarget(alive[spectatorIndex]);
    }

    public void PreviousSpectatorTarget()
    {
        var alive = GetAliveSpectatorTargets();
        if (alive.Count == 0)
        {
            return;
        }

        spectatorIndex--;
        if (spectatorIndex < 0) spectatorIndex = alive.Count - 1;
        FocusCameraOnTarget(alive[spectatorIndex]);
    }

    public void SwitchSpectatorTarget()
    {
        var alive = GetAliveSpectatorTargets();
        if (alive.Count == 0)
        {
            return;
        }

        int index = Random.Range(0, alive.Count);
        FocusCameraOnTarget(alive[index]);
    }

    private void FocusCameraOnTarget(Transform target)
    {
        if (mainCamera != null && target != null)
        {
            mainCamera.Follow = target;
            mainCamera.LookAt = target;

            var killScore = target.GetComponent<KillScoreDisplay>();
            if (killScore != null)
            {
                zoomController.SetUp(killScore);
            }
        }
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

    public void HidePlayerPreview()
    {
        if (playerPreview != null)
            playerPreview.SetActive(false);
    }

    public void ShowPlayerPreview()
    {
        if (playerPreview != null)
            playerPreview.SetActive(true);
    }

    [ClientRpc]
    public void GameOverTargetClientRpc(ClientRpcParams clientRpcParams = default)
    {
        GameOver();
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestNextSpectatorTargetServerRpc(ServerRpcParams rpcParams = default)
    {
        var alive = GetAliveSpectatorTargets();
        if (alive.Count == 0) return;

        spectatorIndex = (spectatorIndex + 1) % alive.Count;

        var netObj = alive[spectatorIndex].GetComponent<NetworkObject>();
        if (netObj != null)
        {
            var senderId = rpcParams.Receive.SenderClientId;
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId } }
            };
            FocusCameraOnTargetClientRpc(netObj, clientRpcParams);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void RequestPreviousSpectatorTargetServerRpc(ServerRpcParams rpcParams = default)
    {
        var alive = GetAliveSpectatorTargets();
        if (alive.Count == 0) return;

        spectatorIndex--;
        if (spectatorIndex < 0) spectatorIndex = alive.Count - 1;

        var netObj = alive[spectatorIndex].GetComponent<NetworkObject>();
        if (netObj != null)
        {
            var senderId = rpcParams.Receive.SenderClientId;
            var clientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { senderId } }
            };
            FocusCameraOnTargetClientRpc(netObj, clientRpcParams);
        }
    }

    [ClientRpc]
    private void FocusCameraOnTargetClientRpc(NetworkObjectReference targetRef, ClientRpcParams rpcParams = default)
    {
        if (targetRef.TryGet(out NetworkObject netObj))
        {
            Transform t = netObj.transform;
            if (t != null)
            {
                mainCamera.Follow = t;
                mainCamera.LookAt = t;

                var killScore = t.GetComponent<KillScoreDisplay>();
                if (killScore != null)
                {
                    zoomController.SetUp(killScore);
                }
            }
        }
    }

    [ClientRpc]
    private void WinnerClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (gamePlayCanvas != null)
        {
            gamePlayCanvas.OnWinner();
            UIManager.Instance.HideCountText();
            zoomController.baseFOV = 35f;
            zoomController.baseFollowY = 5f;
        }
    }

}