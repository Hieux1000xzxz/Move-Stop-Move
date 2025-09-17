using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class Powerup : NetworkBehaviour
{
    [Header("Config")]
    [SerializeField] public PowerupType type;
    [SerializeField] public float duration = 5f;

    [Header("Refs")]
    [SerializeField] public Collider triggerCollider;
    [SerializeField] public NetworkObject netObject;

    public System.Action OnReleased;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        CharacterBase character = other.GetComponent<CharacterBase>();
        if (character != null)
        {
            // ❌ bỏ StartCoroutine trên server
            // ✅ chỉ gọi RPC để mọi client (kể cả host) tự xử lý
            character.ApplyPowerupClientRpc(type, duration);

            ObjectPool.Instance.ReleasePowerup(gameObject, type);
        }
    }
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (triggerCollider != null)
        {
            triggerCollider.enabled = IsServer;
        }
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
