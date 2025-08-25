using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Preallocation
{
    public GameObject gameObject;
    public int count;
    public bool expandable;
    public ObjectType type;
    public WeaponType weaponType;
}
public enum ObjectType
{
    Enemy,
    Weapon,
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
    List<GameObject> pooledGobjects;

    protected override void Awake()
    {
        base.Awake();
        pooledGobjects = new List<GameObject>();

        foreach (Preallocation item in preAllocations)
        {
            for (int i = 0; i < item.count; ++i)
                pooledGobjects.Add(CreateGobject(item.gameObject));
        }
    }

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
            if (preAllocations[i].gameObject.tag == tag)
                if (preAllocations[i].expandable)
                {
                    GameObject obj = CreateGobject(preAllocations[i].gameObject);
                    pooledGobjects.Add(obj);
                    obj.SetActive(true);
                    return obj;
                }
        }
        return null;
    }

    // Hàm mới: Spawn random enemy từ pool
    public GameObject SpawnRandomEnemy()
    {
        // Lấy danh sách tất cả enemy có sẵn trong pool
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

        // Nếu có enemy available, chọn ngẫu nhiên
        if (availableEnemies.Count > 0)
        {
            GameObject randomEnemy = availableEnemies[Random.Range(0, availableEnemies.Count)];
            randomEnemy.SetActive(true);
            return randomEnemy;
        }

        // Nếu không có enemy available nhưng có thể expand, tạo enemy mới
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

        return null; // Không thể spawn enemy
    }

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
        // Lọc danh sách prefab có type mong muốn
        List<Preallocation> candidates = new List<Preallocation>();
        foreach (var pre in preAllocations)
        {
            if (pre.type == type)
            {
                candidates.Add(pre);
            }
        }

        if (candidates.Count == 0) return null;

        // Chọn ngẫu nhiên 1 prefab trong list
        Preallocation randomPre = candidates[Random.Range(0, candidates.Count)];

        // Spawn từ pool (theo tag để tái sử dụng object có sẵn)
        return Spawn(randomPre.gameObject.tag);
    }
    public GameObject SpawnWeaponByType(WeaponType type)
    {
        // Tìm trong pool object có weaponType đúng
        foreach (var obj in pooledGobjects)
        {
            if (!obj.activeSelf)
            {
                foreach (var pre in preAllocations)
                {
                    if (pre.type == ObjectType.Weapon && pre.weaponType == type
                        && obj.name.Contains(pre.gameObject.name))
                    {
                        obj.SetActive(true);
                        return obj;
                    }
                }
            }
        }

        // Nếu chưa có sẵn thì expand
        foreach (var pre in preAllocations)
        {
            if (pre.type == ObjectType.Weapon && pre.weaponType == type && pre.expandable)
            {
                GameObject newWeapon = CreateGobject(pre.gameObject);
                pooledGobjects.Add(newWeapon);
                newWeapon.SetActive(true);
                return newWeapon;
            }
        }

        return null;
    }

    GameObject CreateGobject(GameObject item)
    {
        GameObject gobject = Instantiate(item, transform);
        gobject.SetActive(false);
        return gobject;
    }
}