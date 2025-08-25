using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("AI Counter")]
    [SerializeField] private TextMeshProUGUI aiCounterText;
    [SerializeField] private string displayFormat = "Enemies Left: {0}";

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        UpdateAICounter();
    }

    private void UpdateAICounter()
    {
        if (GameManager.Instance == null || aiCounterText == null) return;

        int remainingAI = GameManager.Instance.GetRemainingQuota();
        int activeAI = GameManager.Instance.GetActiveAICount();
        int totalRemaining = remainingAI + activeAI;

        if (totalRemaining <= 0)
        {
            aiCounterText.text = "All Enemies Defeated!";
            aiCounterText.color = Color.green;
        }
        else
        {
            aiCounterText.text = string.Format(displayFormat, totalRemaining);
            aiCounterText.color = totalRemaining <= 10 ? Color.red : Color.white;
        }
    }
}