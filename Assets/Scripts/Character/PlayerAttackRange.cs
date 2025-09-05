using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PlayerAttackRange : MonoBehaviour
{
    [SerializeField] private CharacterBase character;
    [SerializeField] private int segments = 60;
    [SerializeField] private LineRenderer line;

    void Awake()
    {
        line.useWorldSpace = true;
        line.loop = true;
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = Color.red;
        line.endColor = Color.red;
    }

    void Update()
    {
        if (character == null) return;
        DrawCircle(character.transform.position, character.currentAttackRange);
    }

    void DrawCircle(Vector3 center, float radius)
    {
        line.positionCount = segments;
        float angleStep = 2f * Mathf.PI / (segments - 1);

        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            line.SetPosition(i, new Vector3(center.x + x, center.y, center.z + z));
        }
    }
}
