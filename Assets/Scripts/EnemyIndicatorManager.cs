using System.Collections.Generic;
using UnityEngine;

public class EnemyIndicatorManager : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private RectTransform canvasRect;
    [SerializeField] private RectTransform indicatorPrefab;
    [SerializeField] private float edgeOffset = 50f;
    [SerializeField] private float detectionRange = 50f;

    private List<Transform> enemies = new List<Transform>();
    private Dictionary<Transform, RectTransform> indicators = new Dictionary<Transform, RectTransform>();
    public static EnemyIndicatorManager Instance;
    private void Awake()
    {
        Instance = this;
    }

    public void RegisterEnemy(Transform enemy)
    {
        if (!enemies.Contains(enemy))
        {
            enemies.Add(enemy);
            RectTransform indicator = Instantiate(indicatorPrefab, canvasRect);
            indicator.gameObject.SetActive(true);
            indicators[enemy] = indicator;
        }
    }

    public void UnregisterEnemy(Transform enemy)
    {
        if (enemies.Contains(enemy))
        {
            enemies.Remove(enemy);
            if (indicators.TryGetValue(enemy, out RectTransform indicator))
            {
                Destroy(indicator.gameObject);
                indicators.Remove(enemy);
            }
        }
    }

    private void Update()
    {
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Transform enemy = enemies[i];
            if (enemy == null)
            {
                UnregisterEnemy(enemy);
                continue;
            }

            Vector3 screenPos = mainCamera.WorldToScreenPoint(enemy.position);
            bool isBehind = screenPos.z < 0;
            if (isBehind) screenPos *= -1;

            bool onScreen = screenPos.x > 0 && screenPos.x < Screen.width && screenPos.y > 0 && screenPos.y < Screen.height && !isBehind;
            if (indicators.TryGetValue(enemy, out RectTransform indicator))
            {
                if (Vector3.Distance(mainCamera.transform.position, enemy.position) > detectionRange)
                {
                    indicator.gameObject.SetActive(false);
                    continue;
                }

                indicator.gameObject.SetActive(!onScreen);
                if (!onScreen)
                {
                    Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
                    Vector3 fromCenter = (screenPos - screenCenter).normalized;

                    float clampedX = Mathf.Clamp(screenPos.x, edgeOffset, Screen.width - edgeOffset);
                    float clampedY = Mathf.Clamp(screenPos.y, edgeOffset, Screen.height - edgeOffset);

                    Vector3 edgePos = new Vector3(clampedX, clampedY, 0);
                    indicator.position = edgePos;

                    float angle = Mathf.Atan2(fromCenter.y, fromCenter.x) * Mathf.Rad2Deg;
                    indicator.rotation = Quaternion.Euler(0, 0, angle + 90f);
                }
            }
        }
    }
}
