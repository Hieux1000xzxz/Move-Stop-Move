using Unity.Netcode;
using UnityEngine;

public class Coin : NetworkBehaviour
{
    [SerializeField] private int value = 100;
    [SerializeField] private Collider col;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private float autoPickupRadius = 0.8f;
    [SerializeField] private float despawnDelay = 3f;

    private bool isCollected;
    private float spawnTime;
    private CharacterBase owner;
    private void Awake()
    {
        ObjectPool.Instance?.RegisterNetworkObject(gameObject, GetComponent<NetworkObject>());
    }
    public void SetOwner(CharacterBase creator)
    {
        owner = creator;
    }
    private void OnEnable()
    {
        isCollected = false;
        spawnTime = Time.time;

        if (col != null) col.enabled = true;
        if (meshRenderer != null) meshRenderer.enabled = true;

        CancelInvoke();
        Invoke(nameof(EnableAutoCollect), 0.5f);
        Invoke(nameof(DespawnSelf), despawnDelay);
    }

    private void EnableAutoCollect()
    {
        TryAutoCollectNearby();
    }
    private void Update()
    {
        if (isCollected) return;
        TryAutoCollectNearby();
    }

    private void TryAutoCollectNearby()
    {
        
        Collider[] hits = Physics.OverlapSphere(transform.position, autoPickupRadius);

        foreach (var hit in hits)
        {
            if (!hit.TryGetComponent(out CharacterBase character)) continue;
            if (character == owner) continue;
            
            if (character.ownerType != CharacterBase.OwnerType.Player) continue;
            if (character.isDead || character.health == null || character.health.IsDead) continue;

            Collect(character);
            break;
        }
    }

    private void Collect(CharacterBase character)
    {
        if (isCollected) return;
        isCollected = true;

        if (col != null) col.enabled = false;
        if (meshRenderer != null) meshRenderer.enabled = false;

        if (IsServer)
        {
            if (character != owner)
            {
                character.SessionCoin.Value += value;
                character.AddCoinToClient(character.OwnerClientId, value);
            }

            ObjectPool.Instance.ReleaseCoin(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void DespawnSelf()
    {
        if (isCollected) return;
        if (!IsServer) return;

        ObjectPool.Instance.ReleaseCoin(gameObject);
    }
}  