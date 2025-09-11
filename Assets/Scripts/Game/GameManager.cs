using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using Unity.Netcode;

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
    [SerializeField] private ShopCanvas shopCanvas;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera mainCamera;

    [Header("Powerup")]
    [SerializeField] private GameObject powerupPrefab;
    [SerializeField] private GameObject weaponGrowPrefab;
    [SerializeField] private Transform[] spawnPoints;

    public FloatingJoystick mainJoystick;

    private List<GameObject> activeAIs = new List<GameObject>();
    private int totalSpawned = 0;
    private int totalKilled = 0;
    private bool isGameStarted = false;
    private Transform lastSpawnPoint = null;
    public bool IsGameStarted => isGameStarted;
    protected void Awake()
    { 
        Instance = this;
        isGameStarted = false;
        currentAIQuota = totalAIQuota;
        shopCanvas.LoadSelectedWeapon();
        DisableGamePlaySystem();
    }
    private void Start()
    {
        if (IsServer)
        {
            InvokeRepeating(nameof(SpawnPowerup), 5f, 12f);
        }
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
        Debug.Log("GameOver called on this client");
        GameOver();
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
    }
    public void BindCameraToPlayer(Transform player)
    {
        if (mainCamera != null)
        {
            mainCamera.Follow = player;
            mainCamera.LookAt = player;
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

    private void SpawnPowerup()
    {
        if (spawnPoints.Length == 0) return;

        // Chọn random spawn point khác lần trước
        Transform spawnPoint;
        do
        {
            int index = Random.Range(0, spawnPoints.Length);
            spawnPoint = spawnPoints[index];
        }
        while (spawnPoints.Length > 1 && spawnPoint == lastSpawnPoint);

        lastSpawnPoint = spawnPoint;

        // Random loại powerup
        PowerupType randomType = (Random.value > 0.5f) ? PowerupType.SpeedBoost : PowerupType.WeaponGrow;

        // Spawn từ pool (server chịu trách nhiệm spawn)
        GameObject obj = ObjectPool.Instance.SpawnPowerup(randomType, spawnPoint);

        Powerup powerup = obj.GetComponent<Powerup>();
        if (powerup != null)
        {
            powerup.SetType(randomType);
        }
    }

}