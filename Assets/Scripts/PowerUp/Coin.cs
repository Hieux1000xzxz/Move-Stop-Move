using Unity.Netcode;
using UnityEngine;

public class Coin : NetworkBehaviour
{
    [SerializeField] private int value = 100;
    [SerializeField] private Collider col;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private float despawnDelay = 3f;

    private bool isCollected;
    private CharacterBase owner;
    private float spawnTime;

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
        Invoke(nameof(DespawnSelf), despawnDelay); 
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCollected) return;
        if (!other.TryGetComponent(out CharacterBase character)) return;
        if (character == owner) return;
        if (character.isDead || character.health == null || character.health.IsDead) return;
        if (character.ownerType != CharacterBase.OwnerType.Player) return;

        if (character.IsOwner)
        {
            HideLocal();

            CollectServerRpc(character.NetworkObject);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void CollectServerRpc(NetworkObjectReference playerRef)
    {
        if (isCollected) return;
        isCollected = true;

        HideCoinClientRpc();

        if (playerRef.TryGet(out NetworkObject netObj) &&
            netObj.TryGetComponent(out CharacterBase character) &&
            !character.isDead)
        {
            character.SessionCoin.Value += value;
        }

        Invoke(nameof(DespawnSelf), 0.2f);
    }

    [ClientRpc]
    private void HideCoinClientRpc()
    {
        HideLocal();
    }

    private void HideLocal()
    {
        if (col != null) col.enabled = false;
        if (meshRenderer != null) meshRenderer.enabled = false;
    }

    private void DespawnSelf()
    {
        if (!IsServer) return;
        ObjectPool.Instance.ReleaseCoin(gameObject);
    }
}
