using UnityEngine;

public class Knife : WeaponBase
{
    public override void Launch(Vector3 dir, GameObject shooter)
    {
        base.Launch(dir, shooter);

        
        if (rb != null)
        {
            rb.angularVelocity = transform.forward * rotateSpeed * Mathf.Deg2Rad;
        }
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
