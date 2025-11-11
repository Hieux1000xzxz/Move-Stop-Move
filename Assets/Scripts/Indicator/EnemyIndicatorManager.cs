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

            if (!indicators.ContainsKey(enemy))
            {
                RectTransform indicator = Instantiate(indicatorPrefab, canvasRect);
                indicator.gameObject.SetActive(true);
                indicators[enemy] = indicator;
            }
            else
            {
                indicators[enemy].gameObject.SetActive(true);
            }
        }
    }

    public void UnregisterEnemy(Transform enemy)
    {
        if (enemies.Remove(enemy))
        {
            if (indicators.TryGetValue(enemy, out RectTransform indicator))
            {
                if (indicator != null)
                {
                    indicator.gameObject.SetActive(false);
                }
            }
        }
    }

    private void Update()
    {
        UpdateEnemyIndicators();
    }

    #region Indicator Update Logic

    private void UpdateEnemyIndicators()
    {
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Transform enemy = enemies[i];

            if (HandleMissingEnemy(enemy, i))
                continue;

            if (!indicators.TryGetValue(enemy, out RectTransform indicator) || indicator == null)
                continue;

            UpdateIndicatorForEnemy(enemy, indicator);
        }
    }

    private bool HandleMissingEnemy(Transform enemy, int index)
    {
        if (enemy != null)
            return false;

        if (indicators.TryGetValue(enemy, out RectTransform ind) && ind != null)
            ind.gameObject.SetActive(false);

        enemies.RemoveAt(index);
        return true;
    }

    private void UpdateIndicatorForEnemy(Transform enemy, RectTransform indicator)
    {
        Vector3 screenPos = mainCamera.WorldToScreenPoint(enemy.position);
        bool isBehind = screenPos.z < 0;
        if (isBehind) screenPos *= -1;

        bool onScreen = IsEnemyOnScreen(screenPos, isBehind);

        if (IsOutOfRange(enemy))
        {
            indicator.gameObject.SetActive(false);
            return;
        }

        indicator.gameObject.SetActive(!onScreen);

        if (!onScreen)
            UpdateIndicatorOffScreen(indicator, screenPos);
    }

    #endregion

    #region Indicator Helpers

    private bool IsEnemyOnScreen(Vector3 screenPos, bool isBehind)
    {
        return screenPos.x > 0 && screenPos.x < Screen.width &&
               screenPos.y > 0 && screenPos.y < Screen.height && !isBehind;
    }

    private bool IsOutOfRange(Transform enemy)
    {
        return Vector3.Distance(mainCamera.transform.position, enemy.position) > detectionRange;
    }

    private void UpdateIndicatorOffScreen(RectTransform indicator, Vector3 screenPos)
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

    #endregion
}