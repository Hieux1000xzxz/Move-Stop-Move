using UnityEngine;
using DG.Tweening;

public class Arrow : WeaponBase
{
    public override void Launch(Vector3 dir, GameObject shooter)
    {
        base.Launch(dir, shooter);

    }

    protected override void ReturnToHand()
    {
        base.ReturnToHand();

        if (rb != null)
        {
            rb.angularVelocity = Vector3.zero;
        }
    }
}