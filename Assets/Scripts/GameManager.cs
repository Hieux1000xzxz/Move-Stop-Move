using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("AI Settings")]
    [SerializeField] private int totalAIQuota = 100;
    [SerializeField] private int currentAIQuota;

    private List<GameObject> activeAIs = new List<GameObject>();
    private int totalSpawned = 0;
    private int totalKilled = 0;

    private void Awake()
    {
        Instance = this;
        currentAIQuota = totalAIQuota;
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

    // Data cho UI
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
}