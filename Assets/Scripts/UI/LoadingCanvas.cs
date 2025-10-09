using UnityEngine;
using TMPro;
using System.Collections;

public class LoadingCanvas : BaseCanvas
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TextMeshProUGUI version;
    [SerializeField] private float bounceSpeed = 6f;   
    [SerializeField] private float bounceAmount = 20f; 
    [SerializeField] private float lifetime = 5f;      

    private float timer;
    private string baseText = "Loading";
    private int dotCount = 0;
    private float dotTimer = 0f;

    protected override void OnOpen()
    {
        timer = 0f;
        dotCount = 0;
        dotTimer = 0f;

        if (loadingText != null)
        {
            loadingText.text = baseText;
        }
    }

    private void Update()
    {
        if (!IsOpen || loadingText == null) return;

       
        dotTimer += Time.deltaTime;
        if (dotTimer >= 0.5f) 
        {
            dotTimer = 0f;
            dotCount = (dotCount + 1) % 4; 
        }

        
        string currentText = baseText + new string('.', dotCount);
        loadingText.text = currentText;

        
        AnimateBounce(currentText);

       
        timer += Time.deltaTime;
        if (timer >= lifetime)
        {
            Close();
        }
    }

    private void AnimateBounce(string text)
    {
        TMP_TextInfo textInfo = loadingText.textInfo;
        loadingText.ForceMeshUpdate(); 

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible) continue;

            int vertexIndex = textInfo.characterInfo[i].vertexIndex;
            int materialIndex = textInfo.characterInfo[i].materialReferenceIndex;
            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

           
            float offset = Mathf.Sin(Time.time * bounceSpeed + i * 0.3f) * bounceAmount;

            vertices[vertexIndex + 0].y += offset;
            vertices[vertexIndex + 1].y += offset;
            vertices[vertexIndex + 2].y += offset;
            vertices[vertexIndex + 3].y += offset;
        }

        
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            loadingText.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }
    }
}
