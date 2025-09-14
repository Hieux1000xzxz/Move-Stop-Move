using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PlayerAttackRange : NetworkBehaviour
{
    private LineRenderer line;
    private CharacterBase character;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        character = GetComponent<CharacterBase>();
    }

    private void Update()
    {
        if (!IsOwner) 
        {
            if (line.enabled) line.enabled = false;
            return;
        }

        if (!line.enabled) line.enabled = true;
        DrawCircle(character.currentAttackRange);
    }

    private void DrawCircle(float radius)
    {
        int points = 50;
        line.positionCount = points + 1;
        line.useWorldSpace = true;

        Vector3 center = transform.position;

        for (int i = 0; i <= points; i++)
        {
            float angle = i * Mathf.PI * 2f / points;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            line.SetPosition(i, new Vector3(center.x + x, center.y + 0.05f, center.z + z));
        }
        line.widthMultiplier = 0.05f;
    }

}
