using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PlayerAttackRange : MonoBehaviour
{
    [SerializeField] private CharacterBase character;
    [SerializeField] private int segments = 60;
    [SerializeField] private LineRenderer line;

    private float lastRadius = -1f;
    private Vector3 lastCenter;

    private float[] cosCache;
    private float[] sinCache;

    void Awake()
    {

        line.useWorldSpace = true;
        line.loop = false;
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;
        line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        line.startColor = Color.red;
        line.endColor = Color.red;

        // Cache sin/cos
        cosCache = new float[segments + 1];
        sinCache = new float[segments + 1];
        float angleStep = 2f * Mathf.PI / segments;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep;
            cosCache[i] = Mathf.Cos(angle);
            sinCache[i] = Mathf.Sin(angle);
        }
    }

    void Update()
    {
        if (character == null) return;

        float radius = character.currentAttackRange;
        Vector3 center = character.transform.position;

        if (!Mathf.Approximately(radius, lastRadius) || center != lastCenter)
        {
            DrawCircle(center, radius);
            lastRadius = radius;
            lastCenter = center;
        }
    }

    void DrawCircle(Vector3 center, float radius)
    {
        line.positionCount = segments + 1;

        for (int i = 0; i <= segments; i++)
        {
            float x = cosCache[i] * radius;
            float z = sinCache[i] * radius;
            line.SetPosition(i, new Vector3(center.x + x, center.y, center.z + z));
        }
    }
}
