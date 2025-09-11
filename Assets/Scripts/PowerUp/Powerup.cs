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

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        CharacterBase character = other.GetComponent<CharacterBase>();
        if (character != null)
        {
            character.ApplyPowerup(type, duration);

            // release về pool thay vì Destroy
            PowerupPool.Instance.Release(gameObject, type);
        }
    }

    public void SetType(PowerupType newType)
    {
        type = newType;
    }

}
