using UnityEngine;
using TMPro;

public class KillScoreDisplay : MonoBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private TextMeshPro textMesh; 
    [SerializeField] private int score = 0;
    [SerializeField] private Color textColor = Color.yellow;
    [SerializeField] private int fontSize = 4;

    [Header("Position Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0, 2f, 0);

    [Header("Rotation Settings")]
    [SerializeField] private bool faceCamera = true;

    private Camera cam;
    public int CurrentScore => score;

    private void Start()
    {
        cam = Camera.main;
        UpdateScoreText();
    }

    private void LateUpdate()
    {
        if (textMesh == null) 
            return;

        float scaleFactor = transform.localScale.y;
        textMesh.transform.position = transform.position + offset * scaleFactor;
        if (faceCamera && cam != null)
        {
            textMesh.transform.rotation = Quaternion.LookRotation(cam.transform.forward);
        }
        UpdateScoreText();
    }


    private void UpdateScoreText()
    {
        textMesh.text = score.ToString();
        textMesh.color = textColor;
        textMesh.fontSize = fontSize;
    }

    public void SetScore(int value)
    {
        score += value;
        UpdateScoreText();
    }
}
