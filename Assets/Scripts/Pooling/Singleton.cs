using Unity.Netcode;
using UnityEngine;

public class Singleton<T> : NetworkBehaviour where T : NetworkBehaviour
{
    private static T instance;
    public static T Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<T>();
            return instance;
        }
    }

    protected virtual void Awake()
    {
        if (instance == null)
            instance = this as T;
        else if (instance != this)
            Destroy(gameObject);
    }
}
