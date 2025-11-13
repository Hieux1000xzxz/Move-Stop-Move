using UnityEngine;
using TMPro;
using System.Collections;

public class LoadingCanvas : BaseCanvas
{
    #region Serialized Fields

    [Header("UI Elements")] [SerializeField]
    private TextMeshProUGUI loadingText;

    [SerializeField] private TextMeshProUGUI version;

    [Header("Animation Settings")] [SerializeField]
    private float bounceSpeed = 6f;

    [SerializeField] private float bounceAmount = 20f;

    [Header("Timing Settings")] [SerializeField]
    private float lifetime = 5f;

    [SerializeField] private float dotUpdateInterval = 0.5f;

    #endregion

    #region Private Fields

    private float timer;
    private string baseText = "Loading";
    private int dotCount = 0;
    private float dotTimer = 0f;
    private const int MAX_DOT_COUNT = 4;

    #endregion

    #region Lifecycle Methods

    protected override void OnOpen()
    {
        InitializeLoadingState();
    }

    private void Update()
    {
        if (!ShouldUpdate()) return;

        UpdateDotAnimation();
        UpdateTextDisplay();
        AnimateBounce();
        CheckLifetime();
    }

    #endregion

    #region Initialization

    private void InitializeLoadingState()
    {
        ResetTimers();
        ResetDotCount();
        SetInitialText();
    }

    private void ResetTimers()
    {
        timer = 0f;
        dotTimer = 0f;
    }

    private void ResetDotCount()
    {
        dotCount = 0;
    }

    private void SetInitialText()
    {
        if (loadingText != null)
        {
            loadingText.text = baseText;
        }
    }

    #endregion

    #region Update Checks

    private bool ShouldUpdate()
    {
        return IsOpen && loadingText != null;
    }

    #endregion

    #region Dot Animation

    private void UpdateDotAnimation()
    {
        dotTimer += Time.deltaTime;

        if (ShouldUpdateDots())
        {
            ResetDotTimer();
            IncrementDotCount();
        }
    }

    private bool ShouldUpdateDots()
    {
        return dotTimer >= dotUpdateInterval;
    }

    private void ResetDotTimer()
    {
        dotTimer = 0f;
    }

    private void IncrementDotCount()
    {
        dotCount = (dotCount + 1) % MAX_DOT_COUNT;
    }

    #endregion

    #region Text Display

    private void UpdateTextDisplay()
    {
        string currentText = GenerateLoadingText();
        loadingText.text = currentText;
    }

    private string GenerateLoadingText()
    {
        return baseText + CreateDotString();
    }

    private string CreateDotString()
    {
        return new string('.', dotCount);
    }

    #endregion

    #region Bounce Animation

    private void AnimateBounce()
    {
        TMP_TextInfo textInfo = PrepareTextForAnimation();
        ApplyBounceToCharacters(textInfo);
        UpdateTextMesh(textInfo);
    }

    private TMP_TextInfo PrepareTextForAnimation()
    {
        TMP_TextInfo textInfo = loadingText.textInfo;
        loadingText.ForceMeshUpdate();
        return textInfo;
    }

    private void ApplyBounceToCharacters(TMP_TextInfo textInfo)
    {
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            if (IsCharacterVisible(textInfo, i))
            {
                ApplyBounceToCharacter(textInfo, i);
            }
        }
    }

    private bool IsCharacterVisible(TMP_TextInfo textInfo, int characterIndex)
    {
        return textInfo.characterInfo[characterIndex].isVisible;
    }

    private void ApplyBounceToCharacter(TMP_TextInfo textInfo, int characterIndex)
    {
        int vertexIndex = GetVertexIndex(textInfo, characterIndex);
        int materialIndex = GetMaterialIndex(textInfo, characterIndex);
        Vector3[] vertices = GetVertices(textInfo, materialIndex);

        float offset = CalculateBounceOffset(characterIndex);
        ApplyVerticalOffset(vertices, vertexIndex, offset);
    }

    private int GetVertexIndex(TMP_TextInfo textInfo, int characterIndex)
    {
        return textInfo.characterInfo[characterIndex].vertexIndex;
    }

    private int GetMaterialIndex(TMP_TextInfo textInfo, int characterIndex)
    {
        return textInfo.characterInfo[characterIndex].materialReferenceIndex;
    }

    private Vector3[] GetVertices(TMP_TextInfo textInfo, int materialIndex)
    {
        return textInfo.meshInfo[materialIndex].vertices;
    }

    private float CalculateBounceOffset(int characterIndex)
    {
        float timeOffset = Time.time * bounceSpeed;
        float characterOffset = characterIndex * 0.3f;
        return Mathf.Sin(timeOffset + characterOffset) * bounceAmount;
    }

    private void ApplyVerticalOffset(Vector3[] vertices, int vertexIndex, float offset)
    {
        vertices[vertexIndex + 0].y += offset;
        vertices[vertexIndex + 1].y += offset;
        vertices[vertexIndex + 2].y += offset;
        vertices[vertexIndex + 3].y += offset;
    }

    private void UpdateTextMesh(TMP_TextInfo textInfo)
    {
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            UpdateMeshAtIndex(textInfo, i);
        }
    }

    private void UpdateMeshAtIndex(TMP_TextInfo textInfo, int index)
    {
        textInfo.meshInfo[index].mesh.vertices = textInfo.meshInfo[index].vertices;
        loadingText.UpdateGeometry(textInfo.meshInfo[index].mesh, index);
    }

    #endregion

    #region Lifetime Management

    private void CheckLifetime()
    {
        timer += Time.deltaTime;

        if (HasExceededLifetime())
        {
            Close();
        }
    }

    private bool HasExceededLifetime()
    {
        return timer >= lifetime;
    }

    #endregion
}