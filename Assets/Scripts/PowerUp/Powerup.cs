using Unity.Netcode;
using UnityEngine;

public class Powerup : NetworkBehaviour
{
    [Header("Config")]
    [SerializeField] private PowerupType type;
    [SerializeField] private float duration = 5f;

    [Header("Refs")]
    [SerializeField] private NetworkObject netObject;

    public System.Action OnReleased;
    private void Awake()
    {
        ObjectPool.Instance.RegisterNetworkObject(gameObject, GetComponent<NetworkObject>());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.TryGetComponent(out CharacterBase character)) return;
        if (!character.IsOwner) return;

        ObjectPool.Instance.ReleasePowerup(gameObject, type);

        if (netObject != null && netObject.IsSpawned)
            character.RequestPickupPowerupServerRpc(netObject, type, duration);
        else
            character.ApplyPowerupLocal(type, duration);
    }
    public void SetType(PowerupType newType)
    {
        type = newType;
    }
    private void OnDisable()
    {
        OnReleased?.Invoke();
        OnReleased = null;
    }
}