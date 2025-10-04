using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PlayerAttackRange : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private LineRenderer line;
    [SerializeField] private CharacterBase character;

    [Header("Settings")]
    [SerializeField] private int circleResolution = 50;
    [SerializeField] private float lineWidth = 0.05f;

    private float lastAttackRange = -1f;
    private Vector3[] unitCirclePoints;
    private Vector3[] cachedCirclePositions;
    private readonly Vector3 offset = new Vector3(0, 0.05f, 0);

    private void Awake()
    {
        PrecomputeUnitCircle();
        cachedCirclePositions = new Vector3[circleResolution];

        line.loop = true;
        line.widthMultiplier = lineWidth;
        line.enabled = false;
    }

    private void Update()
    {
        if (!IsOwner)
        {
            if (line.enabled) line.enabled = false;
            return;
        }

        float currentRange = character.currentAttackRange;

        if (currentRange <= 0f)
        {
            if (line.enabled) line.enabled = false;
            return;
        }
        else if (!line.enabled)
        {
            line.enabled = true;
        }

        if (!Mathf.Approximately(currentRange, lastAttackRange))
        {
            lastAttackRange = currentRange;
            UpdateCircle(currentRange);
        }

        Vector3 pos = transform.position + offset;
        for (int i = 0; i < cachedCirclePositions.Length; i++)
        {
            line.SetPosition(i, pos + cachedCirclePositions[i]);
        }
    }

    private void PrecomputeUnitCircle()
    {
        unitCirclePoints = new Vector3[circleResolution];
        for (int i = 0; i < circleResolution; i++)
        {
            float angle = i * Mathf.PI * 2f / circleResolution;
            unitCirclePoints[i] = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
        }
        line.positionCount = circleResolution;
    }

    private void UpdateCircle(float radius)
    {
        for (int i = 0; i < unitCirclePoints.Length; i++)
        {
            cachedCirclePositions[i] = unitCirclePoints[i] * radius;
        }
    }
}
