using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public class SpawnPointData
{
    public Transform point;
    public float spawnRadius = 10f;
}

public class AISpawner : NetworkBehaviour
{
    [Header("Spawn Settings")] [SerializeField]
    private SpawnPointData[] spawnPoints;

    [SerializeField] private float baseSpawnDelay = 1f;
    [SerializeField] private float delayIncrement = 0.5f;
    [SerializeField] private float maxSpawnDelay = 8f;
    [SerializeField] private int spawnPerWave = 2;
    [SerializeField] private float playerSafeDistance = 10f;
    private readonly Collider[] safeCheckBuffer = new Collider[16];

    private Dictionary<Transform, GameObject> spawnPointAIs = new Dictionary<Transform, GameObject>();
    private Coroutine spawnCoroutine;
    private int waveCount = 0;

    private void Start()
    {
        if (!IsServer) return;

        foreach (var sp in spawnPoints)
            spawnPointAIs[sp.point] = null;
    }

    private void Update()
    {
        if (!IsServer) return;

        CleanupDeadAIs();

        if (!gameObject.activeInHierarchy) return;

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
                toRemove.Add(kvp.Key);
        }

        foreach (Transform point in toRemove)
            spawnPointAIs[point] = null;
    }

    private bool HasEmptyPoints()
    {
        foreach (var kvp in spawnPointAIs)
            if (kvp.Value == null)
                return true;
        return false;
    }

    private void SpawnWave()
    {
        waveCount++;
        spawnCoroutine = StartCoroutine(DelayedSpawn());
    }

    private IEnumerator DelayedSpawn()
    {
        if (!CanStartSpawning())
            yield break;

        float delay = CalculateSpawnDelay();
        List<SpawnPointData> availablePoints = GetAvailableSpawnPoints();

        if (availablePoints.Count == 0)
        {
            StopSpawnCoroutine();
            yield break;
        }

        yield return SpawnWaveWithDelay(availablePoints, delay);

        StopSpawnCoroutine();
    }

    private void StopSpawnCoroutine()
    {
        spawnCoroutine = null;
    }

    private bool CanStartSpawning()
    {
        if (!GameManager.Instance.CanSpawnAI())
        {
            StopSpawnCoroutine();
            return false;
        }

        return true;
    }

    private float CalculateSpawnDelay()
    {
        return Mathf.Min(baseSpawnDelay + (waveCount - 1) * delayIncrement, maxSpawnDelay);
    }

    private List<SpawnPointData> GetAvailableSpawnPoints()
    {
        List<SpawnPointData> availablePoints = new List<SpawnPointData>();
        foreach (var sp in spawnPoints)
        {
            if (spawnPointAIs[sp.point] == null && !IsPlayerInSafeRange(sp))
            {
                availablePoints.Add(sp);
            }
        }

        return availablePoints;
    }

    private IEnumerator SpawnWaveWithDelay(List<SpawnPointData> availablePoints, float delay)
    {
        for (int i = 0; i < spawnPerWave && availablePoints.Count > 0; i++)
        {
            if (!GameManager.Instance.CanSpawnAI()) break;

            int index = Random.Range(0, availablePoints.Count);
            SpawnAtPoint(availablePoints[index].point);
            availablePoints.RemoveAt(index);

            yield return new WaitForSeconds(delay);
        }
    }

    private bool IsPlayerInSafeRange(SpawnPointData sp)
    {
        int count = Physics.OverlapSphereNonAlloc(sp.point.position, playerSafeDistance, safeCheckBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider col = safeCheckBuffer[i];
            if (col == null) continue;

            if (col.CompareTag("Player"))
                return true;
        }

        return false;
    }

    private void SpawnAtPoint(Transform spawnPoint)
    {
        if (!GameManager.Instance.CanSpawnAI()) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        if (IsPlayerInSafeRange(new SpawnPointData { point = spawnPoint, spawnRadius = 0 }))
            return;

        GameObject enemy = ObjectPool.Instance.SpawnRandomEnemy();
        if (enemy == null) return;

        PositionEnemy(enemy, spawnPoint.position, spawnPoint.rotation);
        PrepareEnemy(enemy);

        SyncNetworkObject(enemy, spawnPoint);
    }

    private void PrepareEnemy(GameObject enemy)
    {
        if (!ObjectPool.Instance.TryGetCharacter(enemy, out CharacterBase character))
            return;

        character.ResetState();
        character.ChangeWeapon(character.WeaponType);
    }

    private void PositionEnemy(GameObject enemy, Vector3 position, Quaternion rotation)
    {
        enemy.transform.position = position;
        enemy.transform.rotation = rotation;
    }

    private void SyncNetworkObject(GameObject enemy, Transform spawnPoint)
    {
        if (!ObjectPool.Instance.TryGetNetworkObject(enemy, out NetworkObject netObj) || netObj == null)
            return;

        if (netObj.IsSpawned)
            netObj.Despawn(false);

        enemy.SetActive(true);
        netObj.Spawn(true);

        if (GameManager.Instance.TryRegisterAI(netObj))
        {
            spawnPointAIs[spawnPoint] = enemy;
        }
        else
        {
            netObj.Despawn(false);
            enemy.SetActive(false);
        }
    }
}