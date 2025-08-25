using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class AISpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private int maxActiveCount = 20; // Số AI tối đa đang hoạt động cùng lúc trong spawner này

    [Header("Delayed Spawn Settings")]
    [SerializeField] private float spawnDelay = 2f; // Thời gian delay giữa các đợt spawn
    [SerializeField] private int spawnBatchSize = 1; // Số lượng spawn mỗi đợt

    private List<GameObject> activeAIs = new List<GameObject>();
    private bool isInitialSpawn = true;
    private Coroutine delayedSpawnCoroutine;

    private void Start()
    {
        FillToMax();
        isInitialSpawn = false;
    }

    private void Update()
    {
        // Dọn AI đã disabled
        for (int i = activeAIs.Count - 1; i >= 0; i--)
        {
            if (activeAIs[i] == null || !activeAIs[i].activeInHierarchy)
            {
                if (activeAIs[i] != null)
                {
                    GameManager.Instance.UnregisterAI(activeAIs[i]);
                }
                activeAIs.RemoveAt(i);
            }
        }

        // Spawn thêm nếu thiếu và còn quota
        if (activeAIs.Count < maxActiveCount && GameManager.Instance.CanSpawnAI())
        {
            if (isInitialSpawn)
            {
                SpawnOne();
            }
            else
            {
                // Bắt đầu delayed spawn nếu chưa có coroutine nào đang chạy
                if (delayedSpawnCoroutine == null)
                {
                    delayedSpawnCoroutine = StartCoroutine(DelayedSpawnCoroutine());
                }
            }
        }

        // Dừng spawn nếu hết quota
        if (!GameManager.Instance.CanSpawnAI() && delayedSpawnCoroutine != null)
        {
            StopCoroutine(delayedSpawnCoroutine);
            delayedSpawnCoroutine = null;
            Debug.Log($"Spawner {gameObject.name}: Out of AI quota!");
        }
    }

    private IEnumerator DelayedSpawnCoroutine()
    {
        while (activeAIs.Count < maxActiveCount && GameManager.Instance.CanSpawnAI())
        {
            // Spawn theo batch
            int spawnCount = Mathf.Min(spawnBatchSize, maxActiveCount - activeAIs.Count);

            for (int i = 0; i < spawnCount; i++)
            {
                if (activeAIs.Count >= maxActiveCount || !GameManager.Instance.CanSpawnAI())
                    break;

                SpawnOne();
            }

            // Delay trước khi spawn đợt tiếp theo
            if (activeAIs.Count < maxActiveCount && GameManager.Instance.CanSpawnAI())
            {
                yield return new WaitForSeconds(spawnDelay);
            }
        }

        // Reset coroutine reference khi hoàn thành
        delayedSpawnCoroutine = null;
    }

    private void SpawnOne()
    {
        // Kiểm tra lại quota trước khi spawn
        if (!GameManager.Instance.CanSpawnAI())
        {
            Debug.Log($"Spawner {gameObject.name}: Cannot spawn - out of quota!");
            return;
        }

        // Lấy ngẫu nhiên 1 enemy từ pool
        GameObject enemy = ObjectPool.Instance.SpawnRandom(ObjectType.Enemy);
        if (enemy == null)
        {
            Debug.LogWarning($"Spawner {gameObject.name}: No enemy available from pool!");
            return;
        }

        // Reset trạng thái trước khi spawn
        CharacterBase character = enemy.GetComponent<CharacterBase>();
        if (character != null)
        {
            character.ResetState();
        }

        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        if (NavMesh.SamplePosition(spawnPoint.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            enemy.transform.position = hit.position;
            enemy.transform.rotation = spawnPoint.rotation;

            activeAIs.Add(enemy);
            GameManager.Instance.RegisterAI(enemy);
        }
        else
        {
            Debug.LogWarning($"Spawner {gameObject.name}: Invalid spawn position at {spawnPoint.position}");
            enemy.SetActive(false);
        }
    }

    private void FillToMax()
    {
        while (activeAIs.Count < maxActiveCount && GameManager.Instance.CanSpawnAI())
        {
            SpawnOne();
        }
    }

    // Thông tin debug
    public int GetActiveAICount() { return activeAIs.Count; }
    public int GetMaxActiveCount() { return maxActiveCount; }

    private void OnDestroy()
    {
        // Dọn dẹp coroutine khi object bị destroy
        if (delayedSpawnCoroutine != null)
        {
            StopCoroutine(delayedSpawnCoroutine);
            delayedSpawnCoroutine = null;
        }
    }

    // Debug gizmos
    private void OnDrawGizmos()
    {
        if (spawnPoints == null) return;

        Gizmos.color = GameManager.Instance != null && GameManager.Instance.CanSpawnAI() ? Color.green : Color.red;

        foreach (Transform point in spawnPoints)
        {
            if (point != null)
            {
                Gizmos.DrawWireSphere(point.position, 1f);
            }
        }
    }
}