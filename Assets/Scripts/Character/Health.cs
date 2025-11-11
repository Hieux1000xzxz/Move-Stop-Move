using Unity.Netcode;
using UnityEngine;

public class Health : NetworkBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private int maxHealth = 10;
    [SerializeField] private string deadLayerName = "Dead";
    [SerializeField] private Player playerRef;
    
    public int MaxHealth => maxHealth;
    public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool IsDead => CurrentHealth.Value <= 0;
    private void Awake()
    {
        if (playerRef == null)
            TryGetComponent(out playerRef);

        ObjectPool.Instance.RegisterCharacter(gameObject, GetComponent<CharacterBase>());
    }
    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            CurrentHealth.Value = maxHealth;
        }
        else
        {
            Invoke(nameof(EnsureHealthSynced), 0.1f);
        }

        CurrentHealth.OnValueChanged += OnHealthChanged;
    }

    private new void OnDestroy()
    {
        CurrentHealth.OnValueChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(int oldValue, int newValue)
    {
        if (!IsServer) return;
        if (newValue <= 0)
        {
            HandleDeath();
        }
    }

    private void HandleDeath()
    {

        SetDeadLayer();
        if (IsServer)
        {
            NotifyGameOverToOwner();
            DieClientRpc();
        }
    }
    
    public void ApplyDamage(int dmg)
    {
        if (!IsServer || IsDead) return;

        CurrentHealth.Value -= dmg;
        if (CurrentHealth.Value <= 0)
        {
            CurrentHealth.Value = 0;
            HandleDeath();
        }
    }

    private void SetDeadLayer()
    {
        int deadLayer = LayerMask.NameToLayer(deadLayerName);
        if (deadLayer >= 0)
            gameObject.layer = deadLayer;
    }

    private void NotifyGameOverToOwner()
    {
        if (playerRef != null)
        {
            var ownerClientId = playerRef.OwnerClientId;

            GameManager.Instance.GameOverTargetClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { ownerClientId }
                }
            });
        }
    }
    [ClientRpc]
    private void DieClientRpc()
    {
        if (animator != null)
            animator.Play("Death");

        Invoke(nameof(DisableObject), 1.5f);
    }

    private void DisableObject()
    {
        gameObject.SetActive(false);
    }

    private void EnsureHealthSynced()
    {
    }
}
