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
    private List<GameObject> activeAIs = new List<GameObject>();
    private List<Player> activePlayers = new List<Player>();

    public IReadOnlyList<Player> ActivePlayers => activePlayers;

    public void RegisterPlayerInGame(Player player)
    {
        if (!activePlayers.Contains(player))
        {
            activePlayers.Add(player);
            Debug.Log($"✅ Player {player.name} đã đăng ký. Tổng: {activePlayers.Count}");
        }
    }

    public void UnregisterPlayerInGame(Player player)
    {
        if (activePlayers.Remove(player))
        {
            Debug.Log($"❌ Player {player.name} đã rời game. Còn lại: {activePlayers.Count}");
        }
    }

    public int GetPlayerCount() => activePlayers.Count;

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
    }

    public bool CanSpawnAI()
    {
        return currentAIQuota > 0;
    }

    public void RegisterAI(GameObject ai)
    {
        if (currentAIQuota > 0)
        {
            activeAIs.Add(ai);
            currentAIQuota--;
            totalSpawned++;
        }
    }

    public void UnregisterAI(GameObject ai)
    {
        if (activeAIs.Remove(ai))
        {
            totalKilled++;
        }
    }

    public int GetActiveAICount() { return activeAIs.Count; }
    public int GetRemainingQuota() { return currentAIQuota; }
    public int GetTotalKilled() { return totalKilled; }

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
    }

    public void StartGame()
    {
        isGameStarted = true;
        EnableGamePlaySystem();
        ResetGame();
        UIManager.Instance.CloseAllUI();

        HidePlayerPreview();

    }
    public void GameOver()
    {
        isGameStarted = false;

        DisableGamePlaySystem();
        gamePlayCanvas.OnGameOver();
    }

    [ClientRpc]
    public void GameOverClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (isGameStarted) return;
        isGameStarted = true;

        Debug.Log("GameOver called on this client");
        UIManager.Instance.CloseAllUI();
        GameOver();
    }

    private void DisableGamePlaySystem()
    {
        aiSpawner.enabled = false;
        enemyIndicatorManager.enabled = false;
        interactionCanvas.Hide();
        gamePlayCanvas.Hide();
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

    public void HideMainMenu() {

        if (mainMenuCanvas != null)
        {
            mainMenuCanvas.Hide();
        }
    }
    public void BindJoystick(Player player)
    {
        player.SetJoystick(mainJoystick);
    }
    #region NETCODE
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
    #endregion

    public void StartPowerupSpawning()
    {
        if (!IsServer) return;

        if (powerupRoutine == null)
        {
            Debug.Log("▶️ Bắt đầu coroutine spawn powerups...");
            powerupRoutine = StartCoroutine(SpawnPowerupRoutine());
        }
    }

    public void StopPowerupSpawning()
    {
        if (!IsServer) return;

        if (powerupRoutine != null)
        {
            Debug.Log("⏹ Dừng coroutine spawn powerups");
            StopCoroutine(powerupRoutine);
            powerupRoutine = null;
        }
    }

    private IEnumerator SpawnPowerupRoutine()
    {
        yield return new WaitForSeconds(5f); // delay ban đầu
        while (true)
        {
            SpawnPowerup();
            yield return new WaitForSeconds(12f); // chu kỳ spawn
        }
    }

    private void SpawnPowerup()
    {
        if (!IsServer) return;
        if (spawnPoints.Length == 0)
        {
            Debug.LogError("❌ Không có spawnPoints nào trong GameManager!");
            return;
        }

        int index = Random.Range(0, spawnPoints.Length);
        Transform spawnPoint = spawnPoints[index];

        PowerupType type = (Random.value > 0.5f) ? PowerupType.SpeedBoost : PowerupType.WeaponGrow;
        GameObject obj = ObjectPool.Instance.SpawnPowerup(type, spawnPoint.position, Quaternion.identity);
        if (obj == null) return;

        obj.GetComponent<Powerup>().SetType(type);
        Debug.Log($"✅ Spawn {type} tại {spawnPoint.position}");
    }

    public void HidePlayerPreview()
    {
        if (IsServer)
        {
            HidePlayerPreviewClientRpc();
        }
    }

    [ClientRpc]
    private void HidePlayerPreviewClientRpc()
    {
        if (playerPreview != null)
            playerPreview.SetActive(false);
    }

    public void SpawnOnlineAI(Vector3 pos)
    {
        if (!IsServer) return; // chỉ Host/Server spawn

        GameObject aiObj = ObjectPool.Instance.SpawnRandomEnemy(pos);
        if (aiObj != null)
        {
            var netObj = aiObj.GetComponent<NetworkObject>();
            if (netObj != null && !netObj.IsSpawned)
            {
                netObj.Spawn(true); // sync xuống tất cả client
            }

            RegisterAI(aiObj);
            Debug.Log($"✅ [Online] Spawned AI tại {pos}");
        }
    }


}

