using UnityEngine;

public class EnemyIndicatorBinder : MonoBehaviour
{
    private void OnEnable()
    {
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
