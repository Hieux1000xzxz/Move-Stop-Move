using Unity.Netcode;
using UnityEngine;

public class Health : NetworkBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private int maxHealth = 10;
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
            CurrentHealth.Value = maxHealth;

        CurrentHealth.OnValueChanged += OnHealthChanged;
    }

    private void OnDestroy()
    {
        CurrentHealth.OnValueChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(int oldValue, int newValue)
    {
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
            DieClientRpc();
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
}
