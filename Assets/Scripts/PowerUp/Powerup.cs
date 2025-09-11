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

    public PowerupType Type => type;
    public float Duration => duration;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        CharacterBase character = other.GetComponent<CharacterBase>();
        if (character != null)
        {
            character.ApplyPowerup(type, duration);

            if (netObject != null && netObject.IsSpawned)
            {
                netObject.Despawn();
            }
        }
    }

    public void SetType(PowerupType newType)
    {
        type = newType;
    }
}
