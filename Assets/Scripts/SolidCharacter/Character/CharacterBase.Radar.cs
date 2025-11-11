using UnityEngine;

public partial class CharacterBase
{
    protected virtual void UpdateRadar()
    {
        detectedTarget = FindNearestTarget();
        if (detectedTarget == null) return;

        float distance = DistanceTo(detectedTarget);
        if (distance <= attackRange && !isAttacking)
        {
            attackTarget = detectedTarget;
            ChangeState(CharacterState.Attack);
        }
    }

    protected Transform FindNearestTarget()
    {
        Collider[] targets = GetTargetsInRange();

        Transform nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (Collider target in targets)
        {
            if (!IsValidTarget(target))
                continue;

            CharacterBase otherChar = GetCharacterBase(target);
            if (otherChar == null || otherChar == this)
                continue;

            float distance = GetDistanceToTarget(otherChar);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = otherChar.transform;
            }
        }

        return nearest;
    }

    private Collider[] GetTargetsInRange()
    {
        overlapCount = Physics.OverlapSphereNonAlloc(transform.position, attackRange, overlapResults);
        return overlapResults;
    }

    private bool IsValidTarget(Collider target)
    {
        if (target == null) return false;
        if (target.transform == transform) return false;
        if (!target.gameObject.activeInHierarchy) return false;
        if (!target.CompareTag("Player")) return false;
        return true;
    }

    private CharacterBase GetCharacterBase(Collider target)
    {
        return target.GetComponent<CharacterBase>();
    }

    private float GetDistanceToTarget(CharacterBase otherChar)
    {
        return Vector3.Distance(transform.position, otherChar.transform.position);
    }
}