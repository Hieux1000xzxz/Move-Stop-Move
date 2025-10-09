using UnityEngine;
using System.Collections;

public class EnemyIndicatorBinder : MonoBehaviour
{
    private void OnEnable()
    {
        StartCoroutine(RegisterAfterDelay());
    }

    private IEnumerator RegisterAfterDelay()
    {
        yield return new WaitForEndOfFrame();
        if (EnemyIndicatorManager.Instance != null)
        {
            EnemyIndicatorManager.Instance.RegisterEnemy(transform);
        }
    }

    private void OnDisable()
    {
        if (EnemyIndicatorManager.Instance != null)
        {
            EnemyIndicatorManager.Instance.UnregisterEnemy(transform);
        }
    }
}
