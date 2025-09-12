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

public enum ObjectType
{
    Enemy,
    Weapon,
    Powerup,
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

    // ================== REGION: ENEMY ==================
    #region ENEMY
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
    #endregion

    // ================== REGION: WEAPON ==================
    #region WEAPON
    public GameObject SpawnWeaponByType(WeaponType type, Transform parent = null, bool attachToParent = true)
    {
        GameObject obj = GetInactiveWeapon(type);

        if (obj == null)
        {
            obj = ExpandWeapon(type);
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

    private GameObject GetInactiveWeapon(WeaponType type)
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

    private GameObject ExpandWeapon(WeaponType type)
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
            netObj.Spawn(true);
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
        obj.transform.SetParent(transform);
    }

    private GameObject GetInactivePowerup(PowerupType type)
    {
        foreach (var obj in pooledGobjects)
        {
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

    // ================== REGION: HELPERS ==================
    #region HELPERS
    private GameObject CreateGobject(GameObject item)
    {
        GameObject gobject = Instantiate(item, transform);
        gobject.transform.SetParent(transform);

        gobject.transform.position = new Vector3(9999, 9999, 9999);
        gobject.SetActive(false);
        return gobject;
    }
    #endregion
}
