using UnityEngine;
using UnityEngine.AI;

public class AIController : CharacterBase
{
    [Header("AI Settings")]
    [SerializeField] private LayerMask targetLayer; 
    [SerializeField] private float detectionRange = 15f;
    private Transform target;

    protected override void Update()
    {
        if (target == null || Vector3.Distance(transform.position, target.position) > detectionRange)
        {
            target = FindNearestTarget();
        }

        if (target != null && currentState != CharacterState.Attack)
        {
            agent.isStopped = false;
            agent.SetDestination(target.position);

            if (currentState != CharacterState.Move)
            {
                ChangeState(CharacterState.Move);
            }
        }

        base.Update();
    }

    protected override void CheckForAttack()
    {
        if (target == null || isAttacking) return;

        float distance = Vector3.Distance(transform.position, target.position);

        if (distance <= attackRange)
        {
            attackTarget = target;
            agent.isStopped = true;
            ChangeState(CharacterState.Attack);
        }
        else if (currentState == CharacterState.Attack && distance > attackRange)
        {
            agent.isStopped = false;
            EndAttack();
        }
    }

    public override Vector3 GetMovementInput()
    {
        if (target == null) return Vector3.zero;

        Vector3 dir = target.position - transform.position;
        dir.y = 0f;
        return dir.normalized;
    }

    protected override void EndAttack()
    {
        base.EndAttack();
        agent.isStopped = false;
    }

    private Transform FindNearestTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange, targetLayer);
        Transform nearest = null;
        float minDist = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            if (hit.transform == transform) continue;
            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = hit.transform;
            }
        }

        return nearest;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
