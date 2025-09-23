using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

public class AISpawner : NetworkBehaviour
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
        if (!IsServer) return;
        foreach (Transform point in spawnPoints)
            spawnPointAIs[point] = null;

    }

    private void Update()
    {
        if (!IsServer) return;
        CleanupDeadAIs();
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogError("AISpawner GameObject đã bị disable!");
            return;
        }
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
        waveCount++;
        spawnCoroutine = StartCoroutine(DelayedSpawn());

    }

    private IEnumerator DelayedSpawn()
    {
        if (!GameManager.Instance.CanSpawnAI())
        {
            spawnCoroutine = null;
            yield break;
        }

        float delay = Mathf.Min(baseSpawnDelay + (waveCount - 1) * delayIncrement, maxSpawnDelay);

        foreach (Transform point in spawnPoints)
        {
            if (!GameManager.Instance.CanSpawnAI())
            {
                spawnCoroutine = null;
                yield break;
            }

            if (spawnPointAIs[point] == null)
            {
                SpawnAtPoint(point);

                if (!GameManager.Instance.CanSpawnAI())
                {
                    spawnCoroutine = null;
                    yield break;
                }

                yield return new WaitForSeconds(delay);
            }
        }

        spawnCoroutine = null;
    }

    private void SpawnAtPoint(Transform spawnPoint)
    {
        if (!GameManager.Instance.CanSpawnAI())
            return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        GameObject enemy = ObjectPool.Instance.SpawnRandomEnemy();
        if (enemy == null) return;

        CharacterBase character = enemy.GetComponent<CharacterBase>();
        if (character != null)
            character.ResetState();

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();

        if (NavMesh.SamplePosition(spawnPoint.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            enemy.transform.position = hit.position;
            enemy.transform.rotation = spawnPoint.rotation;

            if (agent != null)
            {
                agent.enabled = false;
                agent.enabled = true;

                agent.Warp(hit.position);
                agent.isStopped = false;
            }

            NetworkObject netObj = enemy.GetComponent<NetworkObject>();
            if (netObj != null && !netObj.IsSpawned)
                netObj.Spawn(true);

            bool registered = GameManager.Instance.TryRegisterAI(enemy);
            if (registered)
            {
                spawnPointAIs[spawnPoint] = enemy;
            }
            else
            {
                if (netObj != null && netObj.IsSpawned)
                    netObj.Despawn();
                enemy.SetActive(false);

                if (spawnPointAIs.ContainsKey(spawnPoint) && spawnPointAIs[spawnPoint] == enemy)
                    spawnPointAIs.Remove(spawnPoint);
            }
        }
        else
        {
            enemy.SetActive(false);
        }
    }

    public int GetActiveAICount()
    {
        int count = 0;
        foreach (var ai in spawnPointAIs.Values)
            if (ai != null) count++;
        return count;
    }
}