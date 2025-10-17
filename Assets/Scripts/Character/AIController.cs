using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class AIController : CharacterBase
{
    [Header("AI Settings")]
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

    private Vector3 lastInterestPoint;
    private float targetFoundTime;
    private float lastDecisionTime;
    private bool isObserving;

    protected override void Start()
    {
        base.Start();
        agent.autoRepath = true;

        StartCoroutine(DelayChangeWeapon());
    }
    private IEnumerator DelayChangeWeapon()
    {
        yield return new WaitUntil(() => IsSpawned && NetworkObject.IsSpawned);

        yield return null;
        ChangeWeapon(weaponType);
    }
    protected override void Update()
    {
        if (!GameManager.Instance || !GameManager.Instance.IsGameStarted)
            return;

        if (!IsServer) return;
        base.Update();

        if (Time.time - lastDecisionTime >= 1f)
            MakeDecision();

        AvoidObstacle();
    }

    protected override void UpdateRadar()
    {
        Transform previousTarget = detectedTarget;
        base.UpdateRadar();

        if (detectedTarget != null && detectedTarget != previousTarget)
        {
            targetFoundTime = Time.time;
        }
    }

    protected override void OnTargetLost(Transform lostTarget)
    {
        base.OnTargetLost(lostTarget);

        if (currentState == CharacterState.Attack)
        {
            isAttacking = false;
            agent.isStopped = false;
            ChangeState(CharacterState.Idle);
        }

        lastInterestPoint = transform.position;
        Invoke(nameof(SetRandomPatrolPoint), 0.5f);
    }

    protected override void OnNewTargetFound(Transform newTarget)
    {
        base.OnNewTargetFound(newTarget);
        targetFoundTime = Time.time;
    }

    protected override void OnTargetSwitched(Transform oldTarget, Transform newTarget)
    {
        base.OnTargetSwitched(oldTarget, newTarget);
        targetFoundTime = Time.time;

        if (currentState == CharacterState.Attack)
        {
            float distanceToNew = Vector3.Distance(transform.position, newTarget.position);
            if (distanceToNew <= attackRange && Random.value < aggressionLevel)
            {
                attackTarget = newTarget;
            }
        }
    }

    protected override void CheckForAttack()
    {
    }

    private void MakeDecision()
    {
        lastDecisionTime = Time.time;

        if (detectedTarget == null || !detectedTarget.gameObject.activeInHierarchy)
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                if (!isObserving && (!agent.pathPending && agent.remainingDistance <= 0.5f))
                    DecideWhenIdle();
            }
            return;
        }

        float distance = Vector3.Distance(transform.position, detectedTarget.position);
        float timeSinceFound = Time.time - targetFoundTime;

        if (distance <= attackRange)
        {
            DecideInCombat();
        }
        else if (timeSinceFound <= attentionSpan)
        {
            DecideNearTarget();
        }
        else
        {
            StartWandering();
        }
    }

    private void DecideInCombat()
    {
        float attackDesire = aggressionLevel - fearLevel + Random.Range(-0.2f, 0.2f);

        if (attackDesire > 0.5f)
        {
            if (currentState != CharacterState.Attack)
            {
                attackTarget = detectedTarget;
                ChangeState(CharacterState.Attack);
            }
        }
        else if (Random.value < fearLevel)
        {
            MoveAway();
        }
        else if (Random.value < curiosityLevel)
        {
            ObserveTarget();
        }
        else
        {
            CircleTarget();
        }
    }

    private void DecideNearTarget()
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return;
        float approachChance = aggressionLevel * (1f - fearLevel);

        if (Random.value < approachChance)
            MoveTo(detectedTarget.position);
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
        Vector3 fleePos = transform.position + (transform.position - detectedTarget.position).normalized * Random.Range(3f, 8f);
        if (TrySetDestination(fleePos, 10f))
        {
            if (Random.value < fearLevel)
                detectedTarget = null;
        }
    }

    private void CircleTarget()
    {
        Vector3 perpendicular = Vector3.Cross((detectedTarget.position - transform.position).normalized, Vector3.up).normalized;
        if (Random.value < 0.5f) perpendicular = -perpendicular;
        Vector3 circlePos = transform.position + perpendicular * Random.Range(2f, 5f);
        TrySetDestination(circlePos, 8f);
    }

    private void ObserveTarget()
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return;
        agent.isStopped = true;
        ChangeState(CharacterState.Idle);
        if (detectedTarget != null)
        {
            Vector3 dir = (detectedTarget.position - transform.position);
            dir.y = 0;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * 2f);
        }
    }

    private void StartWandering()
    {
        SetRandomPatrolPoint();
        detectedTarget = null;
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
        if (rayOrigin == null || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return;

        Vector3 dir = (agent.velocity.sqrMagnitude > 0.05f) ? agent.velocity.normalized : transform.forward;

        if (Physics.SphereCast(rayOrigin.position, 0.5f, dir, out RaycastHit hit, rayDistance, obstacleLayer))
        {
            Vector3 avoidDir = Vector3.Cross(Vector3.up, dir).normalized;
            if (Random.value > 0.5f) avoidDir = -avoidDir;

            Vector3 newTarget = transform.position + avoidDir * 3f;

            if (NavMesh.SamplePosition(newTarget, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.ResetPath();
                agent.SetDestination(navHit.position);
                ChangeState(CharacterState.Move);
            }
        }
    }



    private bool TrySetDestination(Vector3 targetPos, float sampleDistance)
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return false;

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, sampleDistance, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.position);
            ChangeState(CharacterState.Move);
            return true;
        }
        return false;
    }

    protected override void EndAttack(bool cancelByMove = false)
    {
        base.EndAttack(cancelByMove);
        lastInterestPoint = transform.position;
        if (Random.value < 0.4f) detectedTarget = null;
        agent.isStopped = false;
    }

    public override Vector3 GetMovementInput() => Vector3.zero;

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        if (lastInterestPoint != Vector3.zero)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(lastInterestPoint, 1f);
        }
    }

    private void OnEnable()
    {
       GameManager.Instance.RegisterKillScore(this.networkObject, scoreDisplay);
    }

    private void OnDisable()
    {
        GameManager.Instance.UnregisterAI(this.networkObject);
    }
}