using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private int maxHealth = 10;
    [SerializeField] private string deadLayerName = "Dead";
    public bool isDead = false;
    private int currentHealth;
    private void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            currentHealth = 0;

            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        int deadLayer = LayerMask.NameToLayer(deadLayerName);
        if (deadLayer >= 0)
            gameObject.layer = deadLayer;
        if (animator != null)
            animator.Play(("Death"));

        Invoke(nameof(DisableObject), 1.5f);
    }

    private void DisableObject()
    {
        gameObject.SetActive(false);
    }
}
