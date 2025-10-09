using Unity.Netcode;
using UnityEngine;

public class Coin : NetworkBehaviour
{
    [SerializeField] private int value = 100;
    private bool isCollected;
    [SerializeField] private Collider col;
    private void Awake()
    {
        ObjectPool.Instance?.RegisterNetworkObject(gameObject, GetComponent<NetworkObject>());
    }

    private void OnEnable()
    {
        isCollected = false;
        if (col != null) col.enabled = true;
        
        CancelInvoke();
        Invoke(nameof(DespawnSelf), 3f);
    }

    private void DespawnSelf()
    {
        if (isCollected) return; 
        if (!IsServer) return;  

        ObjectPool.Instance.ReleaseCoin(gameObject);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;

        CharacterBase character = other.GetComponent<CharacterBase>();
        if (character == null) return;
        if (character.ownerType != CharacterBase.OwnerType.Player) return;

        isCollected = true;

        if (character.IsOwner)
        {
            if (col != null) col.enabled = false;
            gameObject.SetActive(false);
            CoinManager.Instance.AddCoin(value);
        }

        if (IsServer)
        {
            if (!character.IsOwner && !IsHost)
            {
                character.AddCoinToClient(character.OwnerClientId, value);
            }

            if (col != null) col.enabled = false;
            ObjectPool.Instance.ReleaseCoin(gameObject);
        }
    }

}