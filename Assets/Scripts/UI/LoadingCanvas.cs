using UnityEngine;
using TMPro;
using System.Collections;

public class LoadingCanvas : BaseCanvas
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private float bounceSpeed = 6f;   // tốc độ nảy
    [SerializeField] private float bounceAmount = 20f; // biên độ nảy
    [SerializeField] private float lifetime = 5f;      // thời gian tồn tại (giây)

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

        // hiệu ứng dấu ...
        dotTimer += Time.deltaTime;
        if (dotTimer >= 0.5f) // mỗi 0.5s thêm một dấu .
        {
            dotTimer = 0f;
            dotCount = (dotCount + 1) % 4; // 0 -> 1 -> 2 -> 3 rồi quay lại
        }

        // cập nhật text
        string currentText = baseText + new string('.', dotCount);
        loadingText.text = currentText;

        // hiệu ứng nảy từng chữ
        AnimateBounce(currentText);

        // kiểm tra lifetime
        timer += Time.deltaTime;
        if (timer >= lifetime)
        {
            Close();
        }
    }

    private void AnimateBounce(string text)
    {
        TMP_TextInfo textInfo = loadingText.textInfo;
        loadingText.ForceMeshUpdate(); // cập nhật lưới chữ

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            if (!textInfo.characterInfo[i].isVisible) continue;

            int vertexIndex = textInfo.characterInfo[i].vertexIndex;
            int materialIndex = textInfo.characterInfo[i].materialReferenceIndex;
            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

            // nảy theo index
            float offset = Mathf.Sin(Time.time * bounceSpeed + i * 0.3f) * bounceAmount;

            vertices[vertexIndex + 0].y += offset;
            vertices[vertexIndex + 1].y += offset;
            vertices[vertexIndex + 2].y += offset;
            vertices[vertexIndex + 3].y += offset;
        }

        // apply thay đổi
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            loadingText.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
        }
    }
}
