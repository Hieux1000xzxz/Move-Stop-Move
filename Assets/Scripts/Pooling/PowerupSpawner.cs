using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class PowerupSpawner : NetworkBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;

    private Dictionary<Transform, GameObject> activePowerups = new Dictionary<Transform, GameObject>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            SpawnAllOnce();
        }
    }

    private void SpawnAllOnce()
    {
        foreach (var point in spawnPoints)
        {
            if (!activePowerups.ContainsKey(point) || activePowerups[point] == null)
            {
                SpawnAtPoint(point);
            }
        }
    }

    private void SpawnAtPoint(Transform point)
    {
        PowerupType type = (Random.value > 0.5f) ? PowerupType.SpeedBoost : PowerupType.WeaponGrow;

        GameObject obj = ObjectPool.Instance.SpawnPowerup(type, point.position, point.rotation);
        if (obj != null)
        {
            Powerup pu = obj.GetComponent<Powerup>();
            pu.SetType(type);

            pu.OnReleased = () =>
            {
                activePowerups[point] = null;
                StartCoroutine(RespawnAfterDelay(point, 5.0f));
            };

            activePowerups[point] = obj;
        }
    }

    private System.Collections.IEnumerator RespawnAfterDelay(Transform point, float delayTime)
    {
        yield return new WaitForSeconds(delayTime);
        SpawnAtPoint(point);
    }
}
