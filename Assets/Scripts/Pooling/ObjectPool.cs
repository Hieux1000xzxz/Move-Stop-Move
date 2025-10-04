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

    [SerializeField]
    private List<GameObject> pooledGobjects;

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


    // ================== REGION: ENEMY ==================
    #region ENEMY
    public GameObject SpawnRandomEnemy(Vector3 pos = default, Quaternion rot = default)
    {
        GameObject enemy = null;
        
        List<GameObject> availableEnemies = new List<GameObject>();
        foreach (var obj in pooledGobjects)
        {
            if (obj == null) continue; 

            if (!obj.activeSelf)
            {
                foreach (var pre in preAllocations)
                {
                    if (pre.type == ObjectType.Enemy && obj.name.Contains(pre.gameObject.name))
                    {
                        availableEnemies.Add(obj);
                        break;
                    }
                }
            }
        }


        if (availableEnemies.Count > 0)
        {
            enemy = availableEnemies[Random.Range(0, availableEnemies.Count)];
        }
        else
        {
            foreach (var pre in preAllocations)
            {
                if (pre.type == ObjectType.Enemy && pre.expandable)
                {
                    enemy = CreateGobject(pre.gameObject);
                    pooledGobjects.Add(enemy);
                    break;
                }
            }
        }

        if (enemy == null)
        {
            return null;
        }

        // setup transform
        enemy.transform.position = pos;
        enemy.transform.rotation = rot;
        //enemy.SetActive(true);

        return enemy;
    }
    #endregion

    // ================== REGION: WEAPON ==================
    #region WEAPON

    public GameObject SpawnPlayerWeaponByType(WeaponType type, Transform parent = null, bool attachToParent = true)
    {
        return SpawnWeaponInternal(type, ObjectType.Weapon, parent, attachToParent);
    }

    public GameObject SpawnAIWeaponByType(WeaponType type, Transform parent = null, bool attachToParent = true)
    {
        return SpawnWeaponInternal(type, ObjectType.Weapon1, parent, attachToParent);
    }

    private GameObject SpawnWeaponInternal(WeaponType type, ObjectType objType, Transform parent, bool attachToParent)
    {
        GameObject obj = GetInactiveWeapon(type, objType);

        if (obj == null)
        {
            obj = ExpandWeapon(type, objType);
        }

        if (obj == null) return null;
    
        if (parent != null)
        {
            obj.transform.position = parent.position;
            obj.transform.rotation = parent.rotation;
        }

        obj.SetActive(true);
        
        return obj;
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
                    {
                        return obj;
                    }

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


    // ================== REGION: POWERUP ==================
    #region POWERUP
    public GameObject SpawnPowerup(PowerupType type, Vector3 pos, Quaternion rot)
    {
        GameObject obj = GetInactivePowerup(type);

        if (obj == null)
        {
            obj = ExpandPowerup(type);
        }

        if (obj == null) return null;

        obj.transform.position = pos;
        obj.transform.rotation = rot;
        obj.SetActive(true);

        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned && NetworkManager.Singleton.IsServer)
        {
            netObj.Spawn(false);
        }

        return obj;
    }

    public void ReleasePowerup(GameObject obj, PowerupType type)
    {
        if (obj == null) return;

        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned && NetworkManager.Singleton.IsServer)
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
    
    public GameObject SpawnCoin(Vector3 pos, Quaternion rot)
    {
        GameObject obj = null;

        // tìm coin chưa active
        for (int i = pooledGobjects.Count - 1; i >= 0; i--)
        {
            var go = pooledGobjects[i];
            if (go == null)
            {
                pooledGobjects.RemoveAt(i);
                continue;
            }

            if (!go.activeSelf)
            {
                foreach (var pre in preAllocations)
                {
                    if (pre.type == ObjectType.Coin && go.name.Contains(pre.gameObject.name))
                    {
                        obj = go;
                        break;
                    }
                }
            }
            if (obj != null) break;
        }

        // nếu không có thì tạo thêm
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

        obj.transform.position = pos;
        obj.transform.rotation = rot;
        obj.SetActive(true);

        var netObj = obj.GetComponent<NetworkObject>();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (NetworkManager.Singleton.IsServer && netObj != null && !netObj.IsSpawned)
            {
                netObj.Spawn(true); // spawn cho client thấy
            }
        }
        else
        {
            // Single player mode
            obj.SetActive(true);
        }

        return obj;
    }

    public void ReleaseWeapon(GameObject obj)
    {
        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned && NetworkManager.Singleton.IsServer)
        {
            netObj.Despawn(true);
        }

        obj.SetActive(false);
    }
    public void ReleaseCoin(GameObject obj)
    {
        if (obj == null) return;

        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned && NetworkManager.Singleton.IsServer)
        {
            netObj.Despawn(false);
        }

        obj.SetActive(false);
    }

    // ================== REGION: HELPERS ==================
    #region HELPERS
    private GameObject CreateGobject(GameObject item)
    {
        GameObject gobject = Instantiate(item);
        gobject.SetActive(false);
        return gobject;
    }

    #endregion
}
