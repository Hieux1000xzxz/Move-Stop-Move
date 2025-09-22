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
    [SerializeField] private KillScoreDisplay killScoreDisplay;

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
    private List<GameObject> activeAIs = new List<GameObject>();
    private List<Player> activePlayers = new List<Player>();
    private List<NetworkObject> activeEntities = new List<NetworkObject>();
    private int spectatorIndex = 0;

    public NetworkVariable<int> ActiveAICount = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> ActivePlayerCount = new NetworkVariable<int>(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> RemainingAIQuota = new NetworkVariable<int>(
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
            ActiveAICount.OnValueChanged += OnAICountChanged;
            ActivePlayerCount.OnValueChanged += OnPlayerCountChanged;
            RemainingAIQuota.OnValueChanged += OnQuotaChanged;
            //UIManager.Instance?.SendAICountUpdate(ActiveAICount.Value);
            UIManager.Instance?.SendPlayerCountUpdate(ActivePlayerCount.Value);
            UIManager.Instance?.SendQuotaUpdate(RemainingAIQuota.Value);
        }
    }

    private void OnDestroy()
    {
        if (IsClient)
        {
            ActiveAICount.OnValueChanged -= OnAICountChanged;
            ActivePlayerCount.OnValueChanged -= OnPlayerCountChanged;
            RemainingAIQuota.OnValueChanged -= OnQuotaChanged;
        }
    }

    private void OnAICountChanged(int prev, int current)
    {
        UIManager.Instance?.SendAICountUpdate(current);
    }

    private void OnPlayerCountChanged(int prev, int current)
    {
        UIManager.Instance?.SendPlayerCountUpdate(current);
    }

    private void OnQuotaChanged(int prev, int current)
    {
        UIManager.Instance?.SendQuotaUpdate(current);
    }
    public bool CanSpawnAI()
    {
        return currentAIQuota > 0;
    }

    public void RegisterAI(GameObject ai)
    {
        if (IsServer && currentAIQuota > 0)
        {
            activeAIs.Add(ai);
            currentAIQuota--;
            totalSpawned++;
            ActiveAICount.Value = activeAIs.Count;
            RemainingAIQuota.Value = currentAIQuota;
            var netObj = ai.GetComponent<NetworkObject>();
            if (netObj != null && !activeEntities.Contains(netObj))
            {
                activeEntities.Add(netObj);
            }
        }
    }


    public void UnregisterAI(GameObject ai)
    {
        if (IsServer && activeAIs.Remove(ai))
        {
            totalKilled++;
            ActiveAICount.Value = activeAIs.Count;
            var netObj = ai.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                activeEntities.Remove(netObj);
            }
            CheckLastSurvivor();
        }
    }

    public void RegisterPlayerInGame(Player player)
    {
        activePlayers.Add(player);
        ActivePlayerCount.Value = activePlayers.Count;
        var netObj = player.GetComponent<NetworkObject>();
        if (netObj != null && !activeEntities.Contains(netObj))
        {
            activeEntities.Add(netObj);
        }
    }

    public void UnregisterPlayerInGame(Player player)
    {
        if (activePlayers.Remove(player))
        {
            ActivePlayerCount.Value = activePlayers.Count;
            var netObj = player.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                activeEntities.Remove(netObj);
            }
            CheckLastSurvivor();
        }
    }
    private void CheckLastSurvivor()
    {
        if (!IsServer) return;

        if (activeEntities.Count == 1)
        {
            var lastNetObj = activeEntities[0];
            if (lastNetObj != null)
            {
                FocusCameraOnTargetClientRpc(lastNetObj);
                GameWinClientRpc();
            }
        }
    }
    [ClientRpc]
    private void GameWinClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (gamePlayCanvas != null)
        {
            gamePlayCanvas.OnGameWin();
            DisableGamePlaySystem();
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


    public int GetRemainingQuota() { return currentAIQuota; }
    public int GetTotalKilled() { return totalKilled; }

    public int GetRemainingPlayerCount() { return activePlayers.Count; }
    public void ResetGame()
    {
        currentAIQuota = totalAIQuota;
        totalSpawned = 0;
        totalKilled = 0;
        foreach (var ai in activeAIs)
        {
            if (ai != null) ai.SetActive(false);
        }
        activeAIs.Clear();
        ActiveAICount.Value = 0;
        ActivePlayerCount.Value = activePlayers.Count;
        RemainingAIQuota.Value = currentAIQuota;
    }

    public void StartGame()
    {
        isGameStarted = true;
        EnableGamePlaySystem();
        if (IsServer)
        {
            ResetGame();
        }
        UIManager.Instance.CloseAllUI();
        HidePlayerPreview();
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
        killScoreDisplay = killScore;
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

        foreach (var p in activePlayers)
        {
            if (p != null)
            {
                var health = p.GetComponent<Health>();
                if (health != null && !health.IsDead)
                    targets.Add(p.transform);
            }
        }

        if (targets.Count == 0)
        {
            foreach (var ai in activeAIs)
            {
                if (ai != null)
                {
                    var health = ai.GetComponent<Health>();
                    if (health != null && !health.IsDead)
                        targets.Add(ai.transform);
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
            Debug.Log("Không còn ai để spectate.");
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
            Debug.Log("Không còn ai để spectate.");
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
            Debug.Log("Không còn ai để spectate.");
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
            Debug.Log("Không còn ai để spectate.");
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

    public void SpawnOnlineAI(Vector3 pos)
    {
        if (!IsServer) return;
        GameObject aiObj = ObjectPool.Instance.SpawnRandomEnemy(pos);
        if (aiObj != null)
        {
            var netObj = aiObj.GetComponent<NetworkObject>();
            if (netObj != null && !netObj.IsSpawned)
            {
                netObj.Spawn(true);
            }
            RegisterAI(aiObj);
        }
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
            // Gửi target mới về đúng client đang yêu cầu
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
            }
        }
    }



}
