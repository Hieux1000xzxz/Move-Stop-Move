using UnityEngine;

public class Knife : WeaponBase
{
    public override void Launch(Vector3 dir, GameObject shooter)
    {
        base.Launch(dir, shooter);
        StartRotation(); // ✅ chỉ Shield xoay khi bay
    }

    protected override void ReturnToHand()
    {
        base.ReturnToHand();
        StopRotation(); // dừng xoay khi về tay
    }
}
