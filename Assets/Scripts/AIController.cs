using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class AIController : CharacterBase
{
    [Header("AI Settings")]
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private float detectionRange = 15f;
    [SerializeField] private float patrolRadius = 10f;
    [SerializeField] private float observeMinTime = 2f;
    [SerializeField] private float observeMaxTime = 5f;

    [Header("Natural Behavior")]
    [SerializeField] private float aggressionLevel = 0.5f;
    [SerializeField] private float curiosityLevel = 0.7f;
    [SerializeField] private float fearLevel = 0.3f;
    [SerializeField] private float attentionSpan = 5f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private Transform rayOrigin;
    [SerializeField] private float rayDistance = 2f;
    [SerializeField] private LayerMask obstacleLayer;

    private Transform target;
    private Vector3 lastInterestPoint;
    private float targetFoundTime;
    private float lastDecisionTime;
    private bool isObserving;

    protected override void Update()
    {
        UpdateTarget();
        if (Time.time - lastDecisionTime >= 1f) MakeDecision();
        AvoidObstacle();
        base.Update();
    }

    private void UpdateTarget()
    {
        Transform newTarget = FindNearestTarget();
        if (newTarget != target)
        {
            if (target != null && newTarget == null) OnTargetLost();
            target = newTarget;
            targetFoundTime = Time.time;
        }
    }

    private void OnTargetLost()
    {
        if (currentState == CharacterState.Attack)
        {
            isAttacking = false;
            attackTarget = null;
        }
        agent.isStopped = false;
        ChangeState(CharacterState.Idle);
        lastInterestPoint = transform.position;
        Invoke(nameof(SetRandomPatrolPoint), 0.5f);
    }

    private void MakeDecision()
    {
        lastDecisionTime = Time.time;

        if (target == null || !target.gameObject.activeInHierarchy)
        {
            if (!isObserving && (!agent.pathPending && agent.remainingDistance <= 0.5f))
                DecideWhenIdle();
            return;
        }

        float distance = Vector3.Distance(transform.position, target.position);
        float timeSinceFound = Time.time - targetFoundTime;

        if (distance <= attackRange)
            DecideInCombat();
        else if (distance <= detectionRange && timeSinceFound <= attentionSpan)
            DecideNearTarget();
        else
            StartWandering();
    }

    private void DecideInCombat()
    {
        // Fear check
        if (currentState == CharacterState.Attack && Random.value < fearLevel * 0.3f)
        {
            if (Random.value < 0.5f) MoveAway();
            else StartWandering();
            return;
        }

        // Attack or observe
        if (Random.value < aggressionLevel || currentState == CharacterState.Attack)
        {
            if (!isAttacking)
            {
                attackTarget = target;
                agent.isStopped = true;
                ChangeState(CharacterState.Attack);
            }
        }
        else if (Random.value < curiosityLevel)
            ObserveTarget();
        else
            CircleTarget();
    }

    private void DecideNearTarget()
    {
        float approachChance = aggressionLevel * (1f - fearLevel);

        if (Random.value < approachChance)
            MoveTo(target.position);
        else if (Random.value < curiosityLevel)
            ObserveTarget();
        else if (Random.value < 0.3f)
            StartWandering();
    }

    private void DecideWhenIdle()
    {
        float action = Random.value;

        if (action < curiosityLevel * 0.4f)
            StartCoroutine(ObserveRoutine());
        else if (action < 0.7f || lastInterestPoint == Vector3.zero)
            SetRandomPatrolPoint();
        else
            MoveTo(lastInterestPoint);
    }

    private void MoveAway()
    {
        Vector3 fleePos = transform.position + (transform.position - target.position).normalized * Random.Range(3f, 8f);
        if (TrySetDestination(fleePos, 10f))
        {
            if (Random.value < fearLevel) target = null;
        }
    }

    private void CircleTarget()
    {
        Vector3 perpendicular = Vector3.Cross((target.position - transform.position).normalized, Vector3.up).normalized;
        if (Random.value < 0.5f) perpendicular = -perpendicular;
        Vector3 circlePos = transform.position + perpendicular * Random.Range(2f, 5f);
        TrySetDestination(circlePos, 8f);
    }

    private void ObserveTarget()
    {
        agent.isStopped = true;
        ChangeState(CharacterState.Idle);
        if (target != null)
        {
            Vector3 dir = (target.position - transform.position);
            dir.y = 0;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * 2f);
        }
    }

    private void StartWandering()
    {
        SetRandomPatrolPoint();
        target = null;
    }

    private void MoveTo(Vector3 position)
    {
        agent.isStopped = false;
        agent.SetDestination(position);
        ChangeState(CharacterState.Move);
    }

    private void SetRandomPatrolPoint()
    {
        Vector3 randomPos = transform.position + Random.insideUnitSphere * patrolRadius;
        TrySetDestination(randomPos, patrolRadius);
    }

    private IEnumerator ObserveRoutine()
    {
        isObserving = true;
        agent.isStopped = true;
        ChangeState(CharacterState.Idle);

        yield return new WaitForSeconds(Random.Range(observeMinTime, observeMaxTime));

        isObserving = false;
        if (Random.value < 0.6f) SetRandomPatrolPoint();
    }

    private void AvoidObstacle()
    {
        if (rayOrigin == null) return;

        Vector3 dir = agent.velocity.sqrMagnitude > 0.01f ? agent.velocity.normalized : transform.forward;
        if (Physics.Raycast(rayOrigin.position, dir, rayDistance, obstacleLayer))
        {
            Vector3 avoidDir = Vector3.Cross(Vector3.up, dir).normalized;
            if (Random.value > 0.5f) avoidDir = -avoidDir;
            TrySetDestination(transform.position + avoidDir * patrolRadius, patrolRadius);
        }
    }

    private bool TrySetDestination(Vector3 targetPos, float sampleDistance)
    {
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.position);
            ChangeState(CharacterState.Move);
            return true;
        }
        return false;
    }

    private Transform FindNearestTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRange, targetLayer);
        Transform nearest = null;
        float minDist = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            if (hit.transform == transform || !hit.gameObject.activeInHierarchy) continue;

            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = hit.transform;
            }
        }
        return nearest;
    }

    protected override void CheckForAttack() { }
    public override Vector3 GetMovementInput() => Vector3.zero;

    protected override void EndAttack()
    {
        base.EndAttack();
        lastInterestPoint = transform.position;
        if (Random.value < 0.4f) target = null;
        agent.isStopped = false;
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        if (lastInterestPoint != Vector3.zero)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(lastInterestPoint, 1f);
        }
    }
}