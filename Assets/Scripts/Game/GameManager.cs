using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    [Header("AI Settings")]
    [SerializeField] private int totalAIQuota = 100;
    [SerializeField] private int currentAIQuota;
    [SerializeField] private AISpawner aiSpawner;
    [SerializeField] private CinemachineZoomController zoomController;
    [SerializeField] private EnemyIndicatorManager enemyIndicatorManager;
    [SerializeField] private PlayerAttackRange playerAttackRange;
    [SerializeField] private GamePlayCanvas gamePlayCanvas;
    [SerializeField] private KillScoreDisplay playerKillScoreDisplay;
    [SerializeField] private InteractionCanvas interactionCanvas;
    [SerializeField] private ShopCanvas shopCanvas;

    [Header("Camera")]
    [SerializeField] private CinemachineCamera mainCamera;

    private List<GameObject> activeAIs = new List<GameObject>();
    private int totalSpawned = 0;
    private int totalKilled = 0;
    private bool isGameStarted = false;
    public bool IsGameStarted => isGameStarted;
    protected override void Awake()
    {
        base.Awake();
        isGameStarted = false;
        currentAIQuota = totalAIQuota;
        shopCanvas.LoadSelectedWeapon();
        DisableGamePlaySystem();
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
    private void DisableGamePlaySystem()
    {
        aiSpawner.enabled = false;
        enemyIndicatorManager.enabled = false;
        playerAttackRange.enabled = false;
        playerKillScoreDisplay.gameObject.SetActive(false);
        interactionCanvas.Hide();
    }

    private void EnableGamePlaySystem()
    {
        aiSpawner.enabled = true;
        zoomController.baseFOV = 60f;
        zoomController.baseFollowY = 15f;
        enemyIndicatorManager.enabled = true;
        playerAttackRange.enabled = true;
        playerKillScoreDisplay.gameObject.SetActive(true);
        interactionCanvas.Show();
    }

    #region Camera
    public void BindCameraToPlayer(Transform player)
    {
        if (mainCamera != null)
        {
            mainCamera.Follow = player;
            mainCamera.LookAt = player;
        }
    }

    #endregion
}