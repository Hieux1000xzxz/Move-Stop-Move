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

    [Header("Obstacle Avoidance")]
    [SerializeField] private Transform rayOrigin; // điểm dưới chân
    [SerializeField] private float rayDistance = 2f;
    [SerializeField] private LayerMask obstacleLayer; // <-- thêm layer obstacle

    [Header("Combat Settings")]
    [SerializeField] private float attackChance = 0.7f; // 70% cơ hội tấn công khi gặp địch

    private Transform target;
    private Vector3 patrolPoint;
    private bool isObserving = false;

    protected override void Update()
    {
        // Nếu có mục tiêu trong tầm
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
        else if (target == null && !isObserving) // Không có mục tiêu thì patrol
        {
            if (!agent.pathPending && agent.remainingDistance <= 0.5f)
            {
                if (Random.value < 0.3f)
                {
                    StartCoroutine(ObserveRoutine());
                }
                else
                {
                    SetRandomPatrolPoint();
                }
            }
        }

        // Kiểm tra tránh vật cản
        AvoidObstacle();

        base.Update();
    }

    protected override void CheckForAttack()
    {
        if (target == null || isAttacking) return;

        float distance = Vector3.Distance(transform.position, target.position);

        if (distance <= attackRange)
        {
            // Mỗi lần vào tầm bắn đều có cơ hội bắn
            if (Random.value <= attackChance)
            {
                // ✅ Có quyết định tấn công
                attackTarget = target;
                agent.isStopped = true;
                ChangeState(CharacterState.Attack);
            }
            else
            {
                // ❌ Không bắn lần này -> cứ tiếp tục Move
                agent.isStopped = false;
                if (currentState != CharacterState.Move)
                    ChangeState(CharacterState.Move);
            }
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
        target = null; // sau khi kết thúc attack -> tìm mục tiêu mới
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

    private void SetRandomPatrolPoint()
    {
        Vector3 randomDir = Random.insideUnitSphere * patrolRadius;
        randomDir += transform.position;
        NavMeshHit hit;

        if (NavMesh.SamplePosition(randomDir, out hit, patrolRadius, NavMesh.AllAreas))
        {
            patrolPoint = hit.position;
            agent.isStopped = false;
            agent.SetDestination(patrolPoint);
            ChangeState(CharacterState.Move);
        }
    }

    private IEnumerator ObserveRoutine()
    {
        isObserving = true;
        agent.isStopped = true;
        ChangeState(CharacterState.Idle);

        float waitTime = Random.Range(observeMinTime, observeMaxTime);
        yield return new WaitForSeconds(waitTime);

        isObserving = false;
        SetRandomPatrolPoint();
    }

    private void AvoidObstacle()
    {
        if (rayOrigin == null) return;

        Vector3 dir = agent.velocity.sqrMagnitude > 0.01f ? agent.velocity.normalized : transform.forward;

        if (Physics.Raycast(rayOrigin.position, dir, out RaycastHit hit, rayDistance, obstacleLayer))
        {
            // Debug để thấy va chạm
            Debug.Log($"{name} gặp obstacle: {hit.collider.name}");

            // Chọn hướng tránh (trái hoặc phải)
            Vector3 avoidDir = Vector3.Cross(Vector3.up, dir).normalized;
            if (Random.value > 0.5f) avoidDir = -avoidDir;

            Vector3 newPos = transform.position + avoidDir * patrolRadius;
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(newPos, out navHit, patrolRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(navHit.position);
            }
        }
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, patrolRadius);

        if (rayOrigin != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(rayOrigin.position, rayOrigin.position + transform.forward * rayDistance);
        }
    }
}
