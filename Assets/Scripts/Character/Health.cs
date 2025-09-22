using Unity.Netcode;
using UnityEngine;

public class Health : NetworkBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] public int maxHealth = 10;
    [SerializeField] private string deadLayerName = "Dead";

    public NetworkVariable<int> CurrentHealth = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public bool IsDead => CurrentHealth.Value <= 0;

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

    private void OnDestroy()
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

    public void ApplyDamage(int amount)
    {
        if (!IsServer) return;
        if (IsDead) return;

        CurrentHealth.Value = Mathf.Max(CurrentHealth.Value - amount, 0);
    }

    [ServerRpc]
    public void TakeDamageServerRpc(int amount)
    {
        ApplyDamage(amount);
    }

    private void HandleDeath()
    {
        int deadLayer = LayerMask.NameToLayer(deadLayerName);
        if (deadLayer >= 0)
            gameObject.layer = deadLayer;

        if (IsServer)
        {
            Player player = GetComponent<Player>();
            if (player != null)
            {
                // Lấy owner của player này
                var ownerClientId = player.OwnerClientId;

                // Gửi GameOver chỉ cho đúng client
                GameManager.Instance.GameOverTargetClientRpc(new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { ownerClientId }
                    }
                });
            }
            else
            {
                GameManager.Instance.UnregisterAI(gameObject);
            }

            DieClientRpc();
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
        if (CurrentHealth.Value <= 0 && !IsServer)
        {
            Debug.LogWarning($"{name} Client thấy health=0, nhưng chờ sync từ server...");
        }
    }
}
