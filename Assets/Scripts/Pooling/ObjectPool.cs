using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[System.Serializable]
public class Preallocation
{
    public GameObject gameObject;
    public int count;
    public bool expandable;
    public ObjectType type;
    public WeaponType weaponType;
    public PowerupType powerupType;
}

public enum WeaponOwner
{
    Player,
    AI
}

public enum ObjectType
{
    Enemy,
    Weapon,
    Weapon1,
    Powerup,
    Coin,
    Other
}

public enum WeaponType
{
    None,
    Arrow,
    Arrow1,
    Axe,
    Axe1,
    Knife,
    Knife1,
    Shield,
    Shield1
}

public class ObjectPool : Singleton<ObjectPool>
{
    public List<Preallocation> preAllocations;
    [SerializeField] private List<GameObject> pooledGobjects;

    private readonly Dictionary<GameObject, NetworkObject> netCache = new();
    private readonly Dictionary<GameObject, CharacterBase> charCache = new();

    protected override void Awake()
    {
        base.Awake();
        pooledGobjects = new List<GameObject>();

        foreach (Preallocation item in preAllocations)
        {
            for (int i = 0; i < item.count; ++i)
            {
                GameObject obj = CreateGobject(item.gameObject);
                obj.SetActive(false);
                pooledGobjects.Add(obj);
            }
        }
    }

    #region ENEMY
    public GameObject SpawnRandomEnemy(Vector3 pos = default, Quaternion rot = default)
    {
        GameObject enemy = GetRandomAvailableEnemy();
        if (enemy == null)
        {
            enemy = CreateExpandableEnemy();
        }

        if (enemy == null) return null;

        SetupEnemyTransform(enemy, pos, rot);
        return enemy;
    }
    
    private GameObject GetRandomAvailableEnemy()
    {
        List<GameObject> availableEnemies = new List<GameObject>();

        foreach (var obj in pooledGobjects)
        {
            if (obj == null || obj.activeSelf) continue;

            if (IsEnemyPrefab(obj))
            {
                availableEnemies.Add(obj);
            }
        }

        if (availableEnemies.Count == 0) return null;
        return availableEnemies[Random.Range(0, availableEnemies.Count)];
    }
    
    private GameObject CreateExpandableEnemy()
    {
        foreach (var pre in preAllocations)
        {
            if (pre.type == ObjectType.Enemy && pre.expandable)
            {
                GameObject newEnemy = CreateGobject(pre.gameObject);
                pooledGobjects.Add(newEnemy);
                return newEnemy;
            }
        }
        return null;
    }

    private bool IsEnemyPrefab(GameObject obj)
    {
        foreach (var pre in preAllocations)
        {
            if (pre.type == ObjectType.Enemy && obj.name.Contains(pre.gameObject.name))
            {
                return true;
            }
        }
        return false;
    }
    
    private void SetupEnemyTransform(GameObject enemy, Vector3 pos, Quaternion rot)
    {
        enemy.transform.SetPositionAndRotation(pos, rot);
    }

    #endregion

    #region WEAPON

    public GameObject SpawnPlayerWeaponByType(WeaponType type, Transform parent = null, bool attachToParent = true)
        => SpawnWeaponInternal(type, ObjectType.Weapon, parent, attachToParent);

    public GameObject SpawnAIWeaponByType(WeaponType type, Transform parent = null, bool attachToParent = true)
        => SpawnWeaponInternal(type, ObjectType.Weapon1, parent, attachToParent);

    private GameObject SpawnWeaponInternal(WeaponType type, ObjectType objType, Transform parent, bool attachToParent)
    {
        GameObject obj = GetInactiveWeapon(type, objType);
        if (obj == null)
            obj = ExpandWeapon(type, objType);
        
        if (obj == null) return null;

        if (parent != null)
            obj.transform.SetPositionAndRotation(parent.position, parent.rotation);

        obj.SetActive(true);
        return obj;
    }
    public WeaponBase SpawnWeaponByOwner(CharacterBase owner, WeaponType type)
    {
        GameObject go = owner.ownerType switch
        {
            CharacterBase.OwnerType.Player => SpawnPlayerWeaponByType(type, owner.WeaponSpawnPoint, true),
            CharacterBase.OwnerType.AI => SpawnAIWeaponByType(type, owner.WeaponSpawnPoint, true),
            _ => null
        };

        if (go == null) return null;
        return go.GetComponent<WeaponBase>();
    }

    private GameObject GetInactiveWeapon(WeaponType type, ObjectType objType)
    {
        for (int i = pooledGobjects.Count - 1; i >= 0; i--)
        {
            var obj = pooledGobjects[i];
            if (obj == null)
            {
                pooledGobjects.RemoveAt(i);
                continue;
            }

            if (!obj.activeSelf)
            {
                foreach (var pre in preAllocations)
                {
                    if (pre.type == objType && pre.weaponType == type && obj.name.StartsWith(pre.gameObject.name))
                        return obj;
                }
            }
        }
        return null;
    }

    private GameObject ExpandWeapon(WeaponType type, ObjectType objType)
    {
        foreach (var pre in preAllocations)
        {
            if (pre.type == objType && pre.weaponType == type && pre.expandable)
            {
                GameObject newWeapon = CreateGobject(pre.gameObject);
                pooledGobjects.Add(newWeapon);
                return newWeapon;
            }
        }
        return null;
    }
    #endregion

    #region POWERUP
    public GameObject SpawnPowerup(PowerupType type, Vector3 pos, Quaternion rot)
    {
        GameObject obj = GetInactivePowerup(type);

        if (obj == null)
        {
            obj = ExpandPowerup(type);
        }

        if (obj == null) return null;

        obj.transform.SetPositionAndRotation(pos, rot); 
        obj.SetActive(true);

        if (netCache.TryGetValue(obj, out var netObj) &&
            !netObj.IsSpawned && NetworkManager.Singleton.IsServer)
        {
            netObj.Spawn(false);
        }

        return obj;
    }

    public void ReleasePowerup(GameObject obj, PowerupType type)
    {
        if (obj == null) return;

        if (netCache.TryGetValue(obj, out var netObj) &&
            netObj.IsSpawned && NetworkManager.Singleton.IsServer)
        {
            netObj.Despawn(false);
        }

        obj.SetActive(false);
    }

    private GameObject GetInactivePowerup(PowerupType type)
    {
        for (int i = pooledGobjects.Count - 1; i >= 0; i--)
        {
            var obj = pooledGobjects[i];
            if (obj == null)
            {
                pooledGobjects.RemoveAt(i);
                continue;
            }

            if (!obj.activeSelf)
            {
                foreach (var pre in preAllocations)
                {
                    if (pre.type == ObjectType.Powerup && pre.powerupType == type &&
                        obj.name.Contains(pre.gameObject.name))
                    {
                        return obj;
                    }
                }
            }
        }
        return null;
    }

    private GameObject ExpandPowerup(PowerupType type)
    {
        foreach (var pre in preAllocations)
        {
            if (pre.type == ObjectType.Powerup && pre.powerupType == type && pre.expandable)
            {
                GameObject newPowerup = CreateGobject(pre.gameObject);
                pooledGobjects.Add(newPowerup);
                return newPowerup;
            }
        }
        return null;
    }
    #endregion

    #region COIN
    public GameObject SpawnCoin(Vector3 pos, Quaternion rot)
    {
        GameObject obj = null;

        foreach (var go in pooledGobjects)
        {
            if (go == null || go.activeSelf) continue;

            foreach (var pre in preAllocations)
            {
                if (pre.type == ObjectType.Coin && go.name.Contains(pre.gameObject.name))
                {
                    obj = go;
                    break;
                }
            }

            if (obj != null) break;
        }

        if (obj == null)
        {
            foreach (var pre in preAllocations)
            {
                if (pre.type == ObjectType.Coin && pre.expandable)
                {
                    obj = CreateGobject(pre.gameObject);
                    pooledGobjects.Add(obj);
                    break;
                }
            }
        }

        if (obj == null) return null;

        obj.transform.SetLocalPositionAndRotation(pos, rot);
        obj.SetActive(true);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (NetworkManager.Singleton.IsServer && netCache.TryGetValue(obj, out var netObj) && !netObj.IsSpawned)
                netObj.Spawn(true);
        }

        return obj;
    }

    public void ReleaseCoin(GameObject obj)
    {
        if (obj == null) return;
        if (!netCache.TryGetValue(obj, out var netObj)) return;

        if (NetworkManager.Singleton.IsServer && netObj.IsSpawned)
            netObj.Despawn(false);

        obj.SetActive(false);
    }
    #endregion

    #region WEAPON_RELEASE
    public void ReleaseWeapon(GameObject obj)
    {
        if (obj == null) return;
        if (!netCache.TryGetValue(obj, out var netObj)) return;

        if (NetworkManager.Singleton.IsServer && netObj.IsSpawned)
            netObj.Despawn(true);

        obj.SetActive(false);
    }
    #endregion

    #region HELPERS
    private GameObject CreateGobject(GameObject item)
    {
        GameObject gobject = Instantiate(item);
        gobject.SetActive(false);
        return gobject;
    }
    
    public bool TryGetCharacter(GameObject obj, out CharacterBase character)
    {
        return charCache.TryGetValue(obj, out character);
    }
    public bool TryGetNetworkObject(GameObject obj, out NetworkObject netObj)
    {
        return netCache.TryGetValue(obj, out netObj);
    }
    
    public void RegisterNetworkObject(GameObject obj, NetworkObject netObj)
    {
        if (obj != null && netObj != null && !netCache.ContainsKey(obj))
            netCache[obj] = netObj;
    }

    public void RegisterCharacter(GameObject obj, CharacterBase character)
    {
        if (obj != null && character != null && !charCache.ContainsKey(obj))
            charCache[obj] = character;
    }

    #endregion
}
