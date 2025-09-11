using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class AISpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float baseSpawnDelay = 1f;
    [SerializeField] private float delayIncrement = 0.5f;
    [SerializeField] private float maxSpawnDelay = 8f;

    private Dictionary<Transform, GameObject> spawnPointAIs = new Dictionary<Transform, GameObject>();
    private bool isFirstWave = true;
    private Coroutine spawnCoroutine;
    private int waveCount = 0;

    private void Start()
    {
        foreach (Transform point in spawnPoints)
            spawnPointAIs[point] = null;

        SpawnWave();
        isFirstWave = false;
    }

    private void Update()
    {
        CleanupDeadAIs();

        if (HasEmptyPoints() && GameManager.Instance.CanSpawnAI() && spawnCoroutine == null)
        {
            SpawnWave();
        }
    }

    private void CleanupDeadAIs()
    {
        List<Transform> toRemove = new List<Transform>();

        foreach (var kvp in spawnPointAIs)
        {
            if (kvp.Value != null && !kvp.Value.activeInHierarchy)
            {
                GameManager.Instance.UnregisterAI(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }

        foreach (Transform point in toRemove)
            spawnPointAIs[point] = null;
    }

    private bool HasEmptyPoints()
    {
        foreach (var kvp in spawnPointAIs)
            if (kvp.Value == null) return true;
        return false;
    }

    private void SpawnWave()
    {
        if (isFirstWave)
        {
            FillAllPoints();
        }
        else
        {
            waveCount++;
            spawnCoroutine = StartCoroutine(DelayedSpawn());
        }
    }

    private void FillAllPoints()
    {
        foreach (Transform point in spawnPoints)
        {
            //if (spawnPointAIs[point] == null && GameManager.Instance.CanSpawnAI())
            //    SpawnAtPoint(point);
        }
    }

    private IEnumerator DelayedSpawn()
    {
        float delay = Mathf.Min(baseSpawnDelay + (waveCount - 1) * delayIncrement, maxSpawnDelay);

        foreach (Transform point in spawnPoints)
        {
            if (spawnPointAIs[point] == null && GameManager.Instance.CanSpawnAI())
            {
                //SpawnAtPoint(point);
                yield return new WaitForSeconds(delay);
            }
        }

        spawnCoroutine = null;
    }

    //private void SpawnAtPoint(Transform spawnPoint)
    //{
    //    if (!GameManager.Instance.CanSpawnAI()) return;

    //    GameObject enemy = ObjectPool.Instance.SpawnRandom(ObjectType.Enemy);
    //    if (enemy == null) return;

    //    CharacterBase character = enemy.GetComponent<CharacterBase>();
    //    if (character != null) character.ResetState();

    //    if (NavMesh.SamplePosition(spawnPoint.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
    //    {
    //        enemy.transform.position = hit.position;
    //        enemy.transform.rotation = spawnPoint.rotation;
    //        spawnPointAIs[spawnPoint] = enemy;
    //        GameManager.Instance.RegisterAI(enemy);
    //    }
    //    else
    //    {
    //        enemy.SetActive(false);
    //    }
    //}

    public int GetActiveAICount()
    {
        int count = 0;
        foreach (var ai in spawnPointAIs.Values)
            if (ai != null) count++;
        return count;
    }

    private void OnDestroy()
    {
        if (spawnCoroutine != null)
            StopCoroutine(spawnCoroutine);
    }
}