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
            Debug.Log($"[PowerupSpawner] SpawnPoint: {point.name} at {point.position}");
            if (!activePowerups.ContainsKey(point) || activePowerups[point] == null)
            {
                SpawnAtPoint(point.position, point.rotation, point);
            }
        }
    }

    private void SpawnAtPoint(Vector3 pos, Quaternion rot, Transform key)
    {
        if (!IsServer) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;
        
        if (activePowerups.ContainsKey(key) && activePowerups[key] != null)
            return;

        GameObject obj = GetPowerupFromPool(pos, rot, out PowerupType type);
        if (obj == null) return;

        SetupNetworkObject(obj);
        SetupPowerup(obj, type, pos, rot, key);

        activePowerups[key] = obj;
    }
    
    #region Sub Functions
    
    private bool CanSpawnAt(Transform key)
    {
        if (!IsServer) return false;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return false;
        if (activePowerups.ContainsKey(key) && activePowerups[key] != null) return false;
        return true;
    }
    
    private GameObject GetPowerupFromPool(Vector3 pos, Quaternion rot, out PowerupType type)
    {
        type = (Random.value > 0.5f) ? PowerupType.SpeedBoost : PowerupType.WeaponGrow;
        return ObjectPool.Instance.SpawnPowerup(type, pos, rot);
    }
    
    private void SetupNetworkObject(GameObject obj)
    {
        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned && NetworkManager.Singleton.IsListening)
        {
            netObj.Spawn(true);
        }
    }
    
    private void SetupPowerup(GameObject obj, PowerupType type, Vector3 pos, Quaternion rot, Transform key)
    {
        Powerup pu = obj.GetComponent<Powerup>();
        pu.SetType(type);

        pu.OnReleased = () =>
        {
            activePowerups[key] = null;

            if (IsServer)
            {
                StartCoroutine(RespawnAfterDelay(pos, rot, key, 2f));
            }
        };
    }

    #endregion
    
    private IEnumerator RespawnAfterDelay(Vector3 pos, Quaternion rot, Transform key, float delayTime)
    {
        yield return new WaitForSeconds(delayTime);

        if (IsServer && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (!activePowerups.ContainsKey(key) || activePowerups[key] == null)
            {
                SpawnAtPoint(pos, rot, key);
            }
        }
    }
}
