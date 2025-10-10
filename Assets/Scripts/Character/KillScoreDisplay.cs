using System;
using TMPro;
using UnityEngine;

public class KillScoreDisplay : MonoBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private TextMeshPro textMesh;
    [SerializeField] private int score = 0;
    [SerializeField] private Color textColor = Color.yellow;
    [SerializeField] private int fontSize = 4;
    [SerializeField] private TextMeshPro playerName;

    [Header("Position Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0, 2f, 0);

    [Header("Rotation Settings")]
    [SerializeField] private bool faceCamera = true;

    private Camera cam;

    private Vector3 lastScale = Vector3.one;
    private Vector3 lastCameraForward;
    private int lastScore = -1;
    private Color lastTextColor;
    private int lastFontSize = -1;

    public event Action<int> OnScoreChanged;
    public int CurrentScore => score;

    private void Start()
    {
        cam = Camera.main;

        lastScale = transform.localScale;
        if (cam != null)
            lastCameraForward = cam.transform.forward;
        lastScore = score - 1;
        lastTextColor = textColor;
        lastFontSize = fontSize;

        UpdateScoreText();
        UpdatePosition();
        UpdateRotation();
    }

    private void LateUpdate()
    {
        if (textMesh == null)
            return;

        bool needsPositionUpdate = false;

        if (transform.localScale != lastScale)
        {
            needsPositionUpdate = true;
            lastScale = transform.localScale;
        }

        if (needsPositionUpdate)
            UpdatePosition();
            UpdateRotation();
    }

    private void UpdatePosition()
    {
        if (textMesh == null) return;

        float scaleFactor = transform.localScale.y;
        textMesh.transform.localPosition = offset * scaleFactor;
    }

    private void UpdateRotation()
    {
        if (textMesh == null || !faceCamera || cam == null) return;

        textMesh.transform.rotation = Quaternion.LookRotation(cam.transform.forward);
    }

    private void UpdateScoreText()
    {
        if (textMesh == null) return;


        if (score != lastScore)
        {
            textMesh.text = score.ToString();
            lastScore = score;
        }

        if (textColor != lastTextColor)
        {
            textMesh.color = textColor;
            lastTextColor = textColor;
        }

        if (fontSize != lastFontSize)
        {
            textMesh.fontSize = fontSize;
            lastFontSize = fontSize;
        }
    }

    public void SetScore(int value)
    {
        if (score == value) return;

        score = value;
        OnScoreChanged?.Invoke(score);
        UpdateScoreText();
    }

    public void SetPlayerName(string name)
    {
        if (playerName != null)
        {
            playerName.text = name;
        }
    }

    public void SetTextColor(Color newColor)
    {
        if (textColor == newColor) return;

        textColor = newColor;
        UpdateScoreText();
    }

    public void SetFontSize(int newSize)
    {
        if (fontSize == newSize) return;

        fontSize = newSize;
        UpdateScoreText();
    }

    public void SetFaceCamera(bool shouldFaceCamera)
    {
        faceCamera = shouldFaceCamera;
        if (faceCamera)
            UpdateRotation();
    }
}