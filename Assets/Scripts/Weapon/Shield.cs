using UnityEngine;
using DG.Tweening;

public class Shield : WeaponBase
{
    //public override void Launch(Vector3 dir, Quaternion rot, GameObject shooter)
    //{
    //    base.Launch(dir, rot, shooter);
    //    StartRotation();
    //}

    protected override void ReturnToHand()
    {
        base.ReturnToHand();
        StopRotation();
    }
}
