using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Collections;

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
        if (!IsServer) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        if (activePowerups.ContainsKey(point) && activePowerups[point] != null)
            return;

        PowerupType type = (Random.value > 0.5f) ? PowerupType.SpeedBoost : PowerupType.WeaponGrow;
        GameObject obj = ObjectPool.Instance.SpawnPowerup(type, point.position, point.rotation);

        if (obj != null)
        {
            var netObj = obj.GetComponent<NetworkObject>();
            if (netObj != null && !netObj.IsSpawned)
            {
                // ✅ Chỉ spawn khi NetworkManager đang lắng nghe
                if (NetworkManager.Singleton.IsListening)
                {
                    netObj.Spawn(true);
                }
                else
                {
                    Debug.LogWarning("Tried to spawn while NetworkManager is not listening.");
                    return;
                }
            }

            Powerup pu = obj.GetComponent<Powerup>();
            pu.SetType(type);

            pu.OnReleased = () =>
            {
                if (IsServer && netObj != null && netObj.IsSpawned && NetworkManager.Singleton.IsListening)
                {
                    netObj.Despawn();
                }

                activePowerups[point] = null;

                if (IsServer)
                {
                    StartCoroutine(RespawnAfterDelay(point, 5.0f));
                }
            };

            activePowerups[point] = obj;
        }
    }

    private IEnumerator RespawnAfterDelay(Transform point, float delayTime)
    {
        yield return new WaitForSeconds(delayTime);

        if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            SpawnAtPoint(point);
        }
    }
}
