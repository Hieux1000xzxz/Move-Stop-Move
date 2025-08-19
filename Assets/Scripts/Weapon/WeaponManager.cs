using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WeaponManager : Singleton<WeaponManager>
{
    [System.Serializable]
    public class WeaponEntry
    {
        public string weaponTag; 
        public WeaponBase weaponPrefab;
    }

    [SerializeField] private List<WeaponEntry> weapons = new List<WeaponEntry>();

    private Dictionary<string, float> weaponDelays = new Dictionary<string, float>();

    protected override void Awake()
    {
        base.Awake(); 
        foreach (var entry in weapons)
        {
            if (entry.weaponPrefab != null && !weaponDelays.ContainsKey(entry.weaponTag))
            {
                weaponDelays.Add(entry.weaponTag, entry.weaponPrefab.attackDelay);
            }
        }
    }

    public void SpawnProjectile(string weaponTag, Vector3 spawnPosition, Vector3 direction, Quaternion rotation, GameObject owner)
    {
        if (!weaponDelays.TryGetValue(weaponTag, out float delay))
        {
            return;
        }

        StartCoroutine(SpawnRoutine(weaponTag, spawnPosition, direction, rotation, delay, owner));
    }

    private IEnumerator SpawnRoutine(string weaponTag, Vector3 spawnPosition, Vector3 direction, Quaternion rotation, float delay, GameObject owner)
    {
        yield return new WaitForSeconds(delay);

        GameObject obj = ObjectPool.Instance.Spawn(weaponTag);
        if (obj != null)
        {
            obj.transform.position = spawnPosition;
            obj.transform.rotation = rotation;

            WeaponBase weapon = obj.GetComponent<WeaponBase>();
            if (weapon != null)
            {
                weapon.Launch(direction, owner);
            }
        }
    }
}
