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
            if (!IsPotentialTarget(target))
                continue;

            CharacterBase otherChar = GetCharacterFromCollider(target);
            if (!IsValidCharacterTarget(otherChar))
                continue;

            float distance = GetDistanceToTarget(otherChar);
            if (IsCloserTarget(distance, ref minDistance))
            {
                nearest = otherChar.transform;
            }
        }

        return nearest;
    }

    private bool IsPotentialTarget(Collider target)
    {
        return target != null && target.gameObject.activeInHierarchy && IsValidTarget(target);
    }

    private CharacterBase GetCharacterFromCollider(Collider target)
    {
        return GetCharacterBase(target);
    }

    private bool IsValidCharacterTarget(CharacterBase otherChar)
    {
        return otherChar != null && otherChar != this;
    }

    private bool IsCloserTarget(float distance, ref float minDistance)
    {
        if (distance < minDistance)
        {
            minDistance = distance;
            return true;
        }

        return false;
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