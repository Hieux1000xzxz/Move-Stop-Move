using UnityEngine;
using DG.Tweening;

public class Shield : WeaponBase
{
    private bool isReturning = false;
    [SerializeField] private float returnSpeed = 25f; // tốc độ bay về tay

    protected override void FixedUpdate()
    {
        // Khi đang bay đi, chưa bay ngược, và có owner
        if (isFlying && !isReturning && owner != null)
        {
            float dist = Vector3.Distance(launchPos, transform.position);

            // Nếu bay đến điểm cuối mà không trúng -> bắt đầu bay ngược
            if (dist >= owner.currentAttackRange)
            {
                StartReturn();
            }
        }

        // Khi đang bay ngược -> tự di chuyển về vị trí tay hiện tại
        if (isReturning && spawnPoint != null)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                spawnPoint.position,
                returnSpeed * Time.fixedDeltaTime
            );

            // Nếu tới gần tay -> hoàn tất
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

        // Tiếp tục xoay khiên khi bay về
        StartRotation();
    }
}
