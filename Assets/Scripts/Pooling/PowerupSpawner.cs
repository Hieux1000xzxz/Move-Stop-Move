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
        if (!IsServer || !NetworkManager.Singleton.IsListening)
            return;

        if (activePowerups.ContainsKey(point) && activePowerups[point] != null)
        {
            return;
        }

        PowerupType type = (Random.value > 0.5f) ? PowerupType.SpeedBoost : PowerupType.WeaponGrow;
        GameObject obj = ObjectPool.Instance.SpawnPowerup(type, point.position, point.rotation);

        if (obj != null)
        {
            var netObj = obj.GetComponent<NetworkObject>();
            if (netObj != null && !netObj.IsSpawned)
            {
                netObj.Spawn(true);
            }

            Powerup pu = obj.GetComponent<Powerup>();
            pu.SetType(type);

            pu.OnReleased = () =>
            {
                if (IsServer && NetworkManager.Singleton.IsListening && netObj != null && netObj.IsSpawned)
                {
                    netObj.Despawn();
                }

                activePowerups[point] = null;
                StartCoroutine(RespawnAfterDelay(point, 5.0f));
            };

            activePowerups[point] = obj;
        }
    }




    private System.Collections.IEnumerator RespawnAfterDelay(Transform point, float delayTime)
    {
        yield return new WaitForSeconds(delayTime);

        if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            SpawnAtPoint(point);
        }
    }

}
