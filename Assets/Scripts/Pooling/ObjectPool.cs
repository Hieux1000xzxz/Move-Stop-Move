using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using static CharacterBase;

[System.Serializable]
public class Preallocation
{
    public GameObject gameObject;
    public int count;
    public bool expandable;
    public ObjectType type;
    public WeaponType weaponType;
    public PowerupType powerupType;
    public WeaponOwner weaponOwner;
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
    Powerup,
    Other
}

public enum WeaponType
{
    None,
    Arrow,
    Axe,
    Knife,
    Shield,
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

        // Lấy enemy chưa active
        List<GameObject> availableEnemies = new List<GameObject>();
        foreach (var obj in pooledGobjects)
        {
            if (obj == null) continue; // tránh lỗi MissingReference

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
            // Nếu không còn thì expand
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
            Debug.LogError("❌ Không tìm thấy Enemy prefab trong ObjectPool!");
            return null;
        }

        // setup transform
        enemy.transform.position = pos;
        enemy.transform.rotation = rot;
        enemy.SetActive(true);

        // spawn netcode nếu đang chạy multiplayer
        var netObj = enemy.GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            netObj.Spawn(true); // sync xuống client
        }

        return enemy;
    }
    #endregion

    // ================== REGION: WEAPON ==================
    #region WEAPON

    public GameObject SpawnPlayerWeaponByType(WeaponType type, OwnerType ownerType, Transform parent = null, bool attachToParent = true)
    {
        return SpawnWeaponInternal(type, ObjectType.Weapon, ownerType, parent, attachToParent);
    }

    private GameObject SpawnWeaponInternal(WeaponType type, ObjectType objType, CharacterBase.OwnerType ownerType, Transform parent, bool attachToParent)
    {
        GameObject obj = GetInactiveWeapon(type, objType, ownerType);

        if (obj == null )
        {
            obj = ExpandWeapon(type, objType, ownerType);
        }

        obj.SetActive(true);

        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            netObj.Spawn(true);
        }

        if (parent != null)
        {
            obj.transform.position = parent.position;
            obj.transform.rotation = parent.rotation;
        }

        return obj;
    }


    public GameObject GetInactiveWeapon(WeaponType type, ObjectType objType, CharacterBase.OwnerType ownerType)
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
                    if (pre.type == objType &&
                        pre.weaponType == type &&
                        ((ownerType == CharacterBase.OwnerType.Player && pre.weaponOwner == WeaponOwner.Player) ||
                         (ownerType == CharacterBase.OwnerType.AI && pre.weaponOwner == WeaponOwner.AI)) &&
                        obj.name.StartsWith(pre.gameObject.name))
                    {
                        return obj;
                    }
                }
            }
        }
        return null;
    }


    public GameObject ExpandWeapon(WeaponType type, ObjectType objType, CharacterBase.OwnerType ownerType)
    {
        var desiredOwner = (ownerType == CharacterBase.OwnerType.Player)
            ? WeaponOwner.Player
            : WeaponOwner.AI;

        foreach (var pre in preAllocations)
        {
            if (pre.type == objType &&
                pre.weaponType == type &&
                pre.weaponOwner == desiredOwner &&   // ✅ lọc đúng prefab của Player/AI
                pre.expandable)
            {
                Debug.Log("pre1");
                GameObject newWeapon = CreateGobject(pre.gameObject);
                pooledGobjects.Add(newWeapon);
                return newWeapon;
            }
        }

        // (tùy chọn) log cho dễ debug
        Debug.LogWarning($"[ObjectPool] No expandable prefab for {type} / {ownerType}");
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

    public GameObject ExpandPowerup(PowerupType type)
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
    public void RebuildPool()
    {
        pooledGobjects.Clear();

        foreach (Preallocation item in preAllocations)
        {
            for (int i = 0; i < item.count; ++i)
            {
                pooledGobjects.Add(CreateGobject(item.gameObject));
            }
        }
    }
    public void ClearAll()
    {
        for (int i = pooledGobjects.Count - 1; i >= 0; i--)
        {
            var obj = pooledGobjects[i];
            if (obj == null)
            {
                pooledGobjects.RemoveAt(i);
                continue;
            }

            var netObj = obj.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                // ✅ Despawn khỏi mạng trước khi tắt
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                {
                    netObj.Despawn(false); // true = destroy trên client
                }
                else
                {
                    // fallback nếu netmanager đã tắt
                    netObj.Despawn(true);
                }
            }

            obj.SetActive(false); // disable object trong pool
        }
    }


    //reaease
    public void ReleaseWeapon(GameObject obj)
    {
        Debug.Log("RelseWeapon called");    
        if (obj == null) return;

        var weapon = obj.GetComponent<WeaponBase>();
        if (weapon != null && weapon.Owner != null)
        {
            // Nếu owner vẫn alive thì KHÔNG release
            if (!weapon.Owner.health.IsDead)
            {
                Debug.LogWarning($"⚠️ Tried to release weapon of {weapon.Owner.name} nhưng owner chưa chết.");
                return;
            }
        }

        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned && NetworkManager.Singleton.IsServer)
        {
            netObj.Despawn(true); // despawn khỏi mạng
        }

        obj.SetActive(false);
    }




    // ================== REGION: HELPERS ==================
    #region HELPERS
    private GameObject CreateGobject(GameObject item)
    {
        if (!item.TryGetComponent<NetworkObject>(out var netObj))
        {
            Debug.LogError($"❌ Prefab {item.name} chưa có NetworkObject!");
        }
        else
        {
            //Debug.Log($"✅ Spawn prefab {item.name} với GlobalObjectIdHash {netObj.GlobalObjectIdHash}");
        }

        GameObject gobject = Instantiate(item);
        gobject.transform.position = new Vector3(9999, 9999, 9999);
        gobject.SetActive(false);
        return gobject;
    }


    #endregion
}
