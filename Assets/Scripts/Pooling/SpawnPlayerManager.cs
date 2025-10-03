using UnityEngine;

public class SpawnPlayerManager : MonoBehaviour
{
    public static SpawnPlayerManager Instance;  
    [SerializeField] private Transform[] spawnPoints;

    private void Awake()
    {
        Instance = this;
    }

    public Vector3 GetSpawnPosition(ulong clientId)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return Vector3.zero;
        }
        
        int index = (int)(clientId % (ulong)spawnPoints.Length);
        return spawnPoints[index].position;
    }
}