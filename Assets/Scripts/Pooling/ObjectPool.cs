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
    //public PowerupType powerupType;
}

public enum ObjectType
{
    Enemy,
    Weapon,
    //Powerup,
    Other
}

public enum WeaponType
{
    None,
    Arrow,
    Axe,
    Knife,
    Shield
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
                pooledGobjects.Add(CreateGobject(item.gameObject));
            }
        }
    }

    // ================== SPAWN (tag) ==================
    public GameObject Spawn(string tag)
    {
        for (int i = 0; i < pooledGobjects.Count; ++i)
        {
            if (!pooledGobjects[i].activeSelf && pooledGobjects[i].tag == tag)
            {
                pooledGobjects[i].SetActive(true);
                return pooledGobjects[i];
            }
        }

        for (int i = 0; i < preAllocations.Count; ++i)
        {
            if (preAllocations[i].gameObject.tag == tag && preAllocations[i].expandable)
            {
                GameObject obj = CreateGobject(preAllocations[i].gameObject);
                pooledGobjects.Add(obj);
                obj.SetActive(true);
                return obj;
            }
        }
        return null;
    }
    public GameObject SpawnRandomEnemy()
    {
        List<GameObject> availableEnemies = new List<GameObject>();

        foreach (var obj in pooledGobjects)
        {
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
            GameObject randomEnemy = availableEnemies[Random.Range(0, availableEnemies.Count)];
            randomEnemy.SetActive(true);
            return randomEnemy;
        }

        foreach (var pre in preAllocations)
        {
            if (pre.type == ObjectType.Enemy && pre.expandable)
            {
                GameObject newEnemy = CreateGobject(pre.gameObject);
                pooledGobjects.Add(newEnemy);
                newEnemy.SetActive(true);
                return newEnemy;
            }
        }

        return null;
    }

    // ================== GETTER ==================
    public List<GameObject> GetAllObjects()
    {
        return pooledGobjects;
    }

    public List<GameObject> GetObjectsByType(ObjectType type)
    {
        List<GameObject> result = new List<GameObject>();
        foreach (var obj in pooledGobjects)
        {
            foreach (var pre in preAllocations)
            {
                if (pre.type == type && obj.name.Contains(pre.gameObject.name))
                {
                    result.Add(obj);
                    break;
                }
            }
        }
        return result;
    }

    public GameObject SpawnRandom(ObjectType type)
    {
        List<Preallocation> candidates = new List<Preallocation>();
        foreach (var pre in preAllocations)
        {
            if (pre.type == type)
            {
                candidates.Add(pre);
            }
        }

        if (candidates.Count == 0) return null;

        Preallocation randomPre = candidates[Random.Range(0, candidates.Count)];

        return Spawn(randomPre.gameObject.tag);
    }

    // ================== SPAWN WEAPON ==================
    public GameObject SpawnWeaponByType(WeaponType type, Transform parent = null, bool attachToParent = true)
    {
        GameObject obj = GetInactiveObject(type);

        if (obj == null)
        {
            obj = ExpandPool(type);
        }

        if (obj == null) return null;

        obj.SetActive(true);

        if (parent != null)
        {
            if (attachToParent)
            {
                obj.transform.SetParent(parent, false);
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = Quaternion.identity;
            }
            else
            {
                obj.transform.SetParent(null);
                obj.transform.position = parent.position;
                obj.transform.rotation = parent.rotation;
            }
        }

        return obj;
    }

    // ================== RELEASE WEAPON ==================
    public void ReleaseWeapon(GameObject obj)
    {
        if (obj == null) return;

        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned && NetworkManager.Singleton.IsServer)
        {
            netObj.Despawn(true);
        }

        obj.SetActive(false);
        obj.transform.SetParent(transform); 
    }

    // ================== Helpers ==================
    private GameObject GetInactiveObject(WeaponType type)
    {
        foreach (var obj in pooledGobjects)
        {
            if (!obj.activeSelf)
            {
                foreach (var pre in preAllocations)
                {
                    if (pre.type == ObjectType.Weapon && pre.weaponType == type &&
                        obj.name.Contains(pre.gameObject.name))
                    {
                        return obj;
                    }
                }
            }
        }
        return null;
    }

    private GameObject ExpandPool(WeaponType type)
    {
        foreach (var pre in preAllocations)
        {
            if (pre.type == ObjectType.Weapon && pre.weaponType == type && pre.expandable)
            {
                GameObject newWeapon = CreateGobject(pre.gameObject);
                pooledGobjects.Add(newWeapon);
                return newWeapon;
            }
        }
        return null;
    }

    private GameObject CreateGobject(GameObject item)
    {
        GameObject gobject = Instantiate(item, transform);
        gobject.transform.SetParent(transform);

        //if (type == ObjectType.Powerup)
        //{
        //    gobject.SetActive(true);
        //}
        //else
        //{
            gobject.transform.position = new Vector3(9999, 9999, 9999);
            gobject.SetActive(false);
        //}

        return gobject;
    }


    //#region Powerup
    //public GameObject SpawnPowerup(PowerupType type, Transform spawnPoint = null)
    //{
    //    GameObject obj = GetInactivePowerup(type);

    //    if (obj == null)
    //    {
    //        obj = ExpandPoolPowerup(type);
    //    }

    //    if (obj == null) return null;

    //    obj.SetActive(true); 

    //    if (spawnPoint != null)
    //    {
    //        obj.transform.SetParent(null);
    //        obj.transform.position = spawnPoint.position;
    //        obj.transform.rotation = spawnPoint.rotation;
    //    }

    //    var netObj = obj.GetComponent<NetworkObject>();
    //    if (netObj != null && !netObj.IsSpawned && NetworkManager.Singleton.IsServer)
    //    {
    //        netObj.Spawn(true);
    //    }

    //    return obj;
    //}


    //private GameObject GetInactivePowerup(PowerupType type)
    //{
    //    foreach (var obj in pooledGobjects)
    //    {
    //        if (!obj.activeSelf)
    //        {
    //            foreach (var pre in preAllocations)
    //            {
    //                if (pre.type == ObjectType.Powerup && pre.powerupType == type &&
    //                    obj.name.Contains(pre.gameObject.name))
    //                {
    //                    return obj;
    //                }
    //            }
    //        }
    //    }
    //    return null;
    //}

    //private GameObject ExpandPoolPowerup(PowerupType type)
    //{
    //    foreach (var pre in preAllocations)
    //    {
    //        if (pre.type == ObjectType.Powerup && pre.powerupType == type && pre.expandable)
    //        {
    //            GameObject newPowerup = CreateGobject(pre.gameObject);
    //            pooledGobjects.Add(newPowerup);
    //            return newPowerup;
    //        }
    //    }
    //    return null;
    //}

    //#endregion
}
