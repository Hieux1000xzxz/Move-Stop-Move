using UnityEngine;
using DG.Tweening;

public class Shield : WeaponBase
{
    public void Launch(Vector3 dir, Quaternion rot, GameObject shooter)
    {
        StartRotation();
    }

    protected override void ReturnToHand()
    {
        base.ReturnToHand();
        StopRotation();
    }
}
