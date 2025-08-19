using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class AISpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private int maxCount = 10; // số lượng AI tối đa cùng lúc

    private List<GameObject> activeAIs = new List<GameObject>();

    private void Start()
    {
        FillToMax();
    }

    private void Update()
    {
        // Xóa AI đã về pool (inactive)
        for (int i = activeAIs.Count - 1; i >= 0; i--)
        {
            if (!activeAIs[i].activeInHierarchy)
            {
                activeAIs.RemoveAt(i);
            }
        }

        // Nếu thiếu thì spawn thêm
        if (activeAIs.Count < maxCount)
        {
            FillToMax();
        }
    }

    private void FillToMax()
    {
        int toSpawn = maxCount - activeAIs.Count;
        for (int i = 0; i < toSpawn; i++)
        {
            SpawnOne();
        }
    }

    private void SpawnOne()
    {
        // Lấy tất cả object từ pool
        var allObjects = ObjectPool.Instance.GetAllObjects();

        // tìm object chưa active
        GameObject pooledObj = null;
        foreach (var obj in allObjects)
        {
            if (!obj.activeInHierarchy)
            {
                pooledObj = obj;
                break;
            }
        }

        if (pooledObj == null) return; // không có object rảnh

        // chọn spawnPoint ngẫu nhiên
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        // đảm bảo nằm trên NavMesh
        NavMeshHit hit;
        if (NavMesh.SamplePosition(spawnPoint.position, out hit, 2f, NavMesh.AllAreas))
        {
            pooledObj.transform.position = hit.position;
            pooledObj.transform.rotation = spawnPoint.rotation;
            pooledObj.SetActive(true);

            activeAIs.Add(pooledObj);

            AIController ai = pooledObj.GetComponent<AIController>();
            if (ai != null)
            {
                //ai.ResetAI(); // reset trạng thái AI nếu bạn có hàm này
            }
        }
        else
        {
            Debug.LogWarning("SpawnPoint " + spawnPoint.name + " không nằm trên NavMesh.");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (spawnPoints != null)
        {
            foreach (var point in spawnPoints)
            {
                if (point != null)
                    Gizmos.DrawSphere(point.position, 0.3f);
            }
        }
    }
}
