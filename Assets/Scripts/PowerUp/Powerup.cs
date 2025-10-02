using Unity.Netcode;
using UnityEngine;

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
        CharacterBase character = other.GetComponent<CharacterBase>();
        if (character == null) return;

        if (character.IsOwner)
        {
            gameObject.SetActive(false);

            if (netObject != null && netObject.IsSpawned) 
            {
                character.RequestPickupPowerupServerRpc(netObject, type, duration);
            }
            
            else
            {
                character.ApplyPowerupLocal(type, duration);
            }
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