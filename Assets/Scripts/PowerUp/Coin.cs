using Unity.Netcode;
using UnityEngine;

public class Coin : NetworkBehaviour
{
    [SerializeField] private int value = 100;
    private bool isCollected;
    [SerializeField] private Collider col;

    private void OnEnable()
    {
        isCollected = false;
        if (col != null) col.enabled = true;
    }

   
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return; 
        if (isCollected) return;

        CharacterBase character = other.GetComponent<CharacterBase>();
        if (character == null) return;

        if (character.ownerType == CharacterBase.OwnerType.Player)
        {
            isCollected = true;
            if (col != null) col.enabled = false;

            character.AddCoinToClient(character.OwnerClientId, value);
            ObjectPool.Instance.ReleaseCoin(gameObject);
        }
    }
}