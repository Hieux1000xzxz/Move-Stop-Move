using System.Collections.Generic;
using UnityEngine;

public class RadarSystem : MonoBehaviour
{
    [Header("Radar Settings")]
    [SerializeField] private LayerMask targetLayers = -1;

    private CharacterBase character;

    private void Awake()
    {
        character = GetComponent<CharacterBase>();
    }

    public Transform GetNearestTarget()
    {
        float range = character != null ? character.currentAttackRange : 5f;
        Collider[] hits = Physics.OverlapSphere(transform.position, range, targetLayers);
        Transform nearest = null;
        float minDistance = Mathf.Infinity;

        foreach (Collider hit in hits)
        {
            if (hit.transform == transform || !hit.gameObject.activeInHierarchy) continue;

            float distance = Vector3.Distance(transform.position, hit.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearest = hit.transform;
            }
        }

        return nearest;
    }

    public List<Transform> GetAllTargets()
    {
        float range = character != null ? character.currentAttackRange : 5f;
        List<Transform> targets = new List<Transform>();
        Collider[] hits = Physics.OverlapSphere(transform.position, range, targetLayers);

        foreach (Collider hit in hits)
        {
            if (hit.transform != transform && hit.gameObject.activeInHierarchy)
                targets.Add(hit.transform);
        }

        return targets;
    }

    public bool HasTargetsInRange()
    {
        float range = character != null ? character.currentAttackRange : 5f;
        Collider[] hits = Physics.OverlapSphere(transform.position, range, targetLayers);

        foreach (Collider hit in hits)
        {
            if (hit.transform != transform && hit.gameObject.activeInHierarchy)
                return true;
        }

        return false;
    }

    public void SetTargetLayers(LayerMask layers)
    {
        targetLayers = layers;
    }

    private void OnDrawGizmosSelected()
    {
        float range = character != null ? character.currentAttackRange : 5f;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}