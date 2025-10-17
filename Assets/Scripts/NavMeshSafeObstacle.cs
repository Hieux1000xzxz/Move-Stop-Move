using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshObstacle))]
public class NavMeshSafeObstacle : MonoBehaviour
{
    private NavMeshObstacle obstacle;

    private void Awake()
    {
        obstacle = GetComponent<NavMeshObstacle>();
    }

    public void DisableAndHide()
    {
        StartCoroutine(DisableRoutine());
    }

    private IEnumerator DisableRoutine()
    {
        if (obstacle != null)
        {
            obstacle.carving = false;
            obstacle.enabled = false;
        }

        yield return null; 
        gameObject.SetActive(false);
    }
}