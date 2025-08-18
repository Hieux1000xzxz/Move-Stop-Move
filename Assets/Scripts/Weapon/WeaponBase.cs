using UnityEngine;
using System.Collections;

public abstract class WeaponBase : MonoBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] protected float speed = 10f;
    [SerializeField] protected float lifetime = 5f;
    [SerializeField] protected int damage = 10;
    [SerializeField] public float attackDelay = 0.2f;
    [SerializeField] protected Rigidbody rb;

    private Coroutine launchRoutine;

    protected virtual void OnEnable()
    {
        Invoke(nameof(Despawn), lifetime);
    }

    public virtual void Launch(Vector3 direction)
    {
        if (launchRoutine != null)
            StopCoroutine(launchRoutine);

        launchRoutine = StartCoroutine(LaunchRoutine(direction));
    }

    private IEnumerator LaunchRoutine(Vector3 direction)
    {
        yield return new WaitForSeconds(attackDelay);
        if (rb != null)
            rb.linearVelocity = direction.normalized * speed;
    }

    /*protected virtual void OnTriggerEnter(Collider other)
    {
        Health target = other.GetComponent<Health>();
        if (target != null)
            target.TakeDamage(damage);

        Despawn();
    }*/

    protected virtual void Despawn()
    {
        CancelInvoke();
        rb.linearVelocity = Vector3.zero;
        gameObject.SetActive(false);
    }
}
