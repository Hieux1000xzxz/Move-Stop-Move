using UnityEngine;

public class WeaponBase : MonoBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] private float speed = 12f;
    [SerializeField] private int damage = 1;
    [SerializeField] private Vector3 handRotationOffset = Vector3.zero; // 👈 offset xoay khi gắn vào tay
    [SerializeField] private float spawnDelay = 0.2f; // 👈 delay khi spawn lại

    private Rigidbody rb;
    private CharacterBase owner;
    private Transform spawnPoint;
    private Vector3 originalPos;
    private Quaternion originalRot;
    private bool isFlying;
    private Vector3 launchPos; // 👈 vị trí bắn ra
    public bool IsFlying => isFlying;
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    public void Init(CharacterBase character, Transform hand)
    {
        owner = character;
        spawnPoint = hand;

        // Lưu vị trí & rotation ban đầu
        originalPos = transform.localPosition;
        originalRot = transform.localRotation;

        ReturnToHand();
    }

    public void Launch(Vector3 dir, GameObject shooter)
    {
        if (isFlying) return;

        transform.SetParent(null);
        rb.isKinematic = false;
        rb.linearVelocity = dir * speed;

        launchPos = transform.position; // lưu vị trí bắt đầu
        isFlying = true;
    }

    private void Update()
    {
        if (isFlying && owner != null)
        {
            float dist = Vector3.Distance(launchPos, transform.position);
            if (dist >= owner.currentAttackRange)
            {
                ReturnToHand();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isFlying || other.gameObject == owner.gameObject) return;

        if (other.CompareTag("Enemy"))
        {
            Health h = other.GetComponent<Health>();
            if (h != null) h.TakeDamage(damage);
            owner.AddScore(1);
            ReturnToHand();
        }
    }

    private void ReturnToHand()
    {
        rb.isKinematic = true;

        transform.SetParent(spawnPoint);

        // giữ nguyên local position gốc
        transform.localPosition = originalPos;
        // áp dụng offset xoay
        transform.localRotation = originalRot * Quaternion.Euler(handRotationOffset);

        isFlying = false;
        if (owner != null) owner.OnWeaponReturned();
    }
}
