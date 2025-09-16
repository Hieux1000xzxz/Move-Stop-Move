using UnityEngine;
using DG.Tweening;

public class Shield : WeaponBase
{
    private bool isReturning = false;
    [SerializeField] private float returnSpeed = 25f;

    protected override void Update()
    {
        if (isFlying && !isReturning && owner != null)
        {
            float dist = Vector3.Distance(launchPos, transform.position);

            if (dist >= owner.currentAttackRange)
            {
                StartReturn();
            }
        }

        if (isReturning && spawnPoint != null)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                spawnPoint.position,
                returnSpeed * Time.fixedDeltaTime
            );

            if (Vector3.Distance(transform.position, spawnPoint.position) < 0.05f)
            {
                ReturnToHand();
                isReturning = false;
            }
        }
    }

    private void StartReturn()
    {
        if (isReturning) return;
        isReturning = true;

        rb.isKinematic = true;

        StartRotation();
    }

    public override void Launch(Vector3 dir, GameObject shooter)
    {
        base.Launch(dir, shooter);
        StartRotation(); // ✅ Shield mới xoay
    }

}
