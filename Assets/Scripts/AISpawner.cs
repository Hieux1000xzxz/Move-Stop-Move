using UnityEngine;
using UnityEngine.AI;

public class AISpawner : MonoBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private Transform[] spawnPoints;

    private void Start()
    {
        SpawnAllFromPool();
    }

    private void SpawnAllFromPool()
    {
        var allObjects = ObjectPool.Instance.GetAllObjects();

        for (int i = 0; i < allObjects.Count; i++)
        {
            if (i >= spawnPoints.Length)
            {
                Debug.LogWarning("Không đủ spawn point cho object: " + allObjects[i].name);
                break;
            }

            GameObject pooledObj = allObjects[i];
            Transform spawnPoint = spawnPoints[i];

            NavMeshHit hit;
            if (NavMesh.SamplePosition(spawnPoint.position, out hit, 2f, NavMesh.AllAreas))
            {
                pooledObj.transform.position = hit.position;
                pooledObj.transform.rotation = spawnPoint.rotation;
                pooledObj.SetActive(true);

                AIController ai = pooledObj.GetComponent<AIController>();
                if (ai != null)
                {
                    // ai.ResetAI();
                }
            }
            else
            {
                Debug.LogWarning("SpawnPoint " + spawnPoint.name + " không nằm trên NavMesh.");
            }
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
