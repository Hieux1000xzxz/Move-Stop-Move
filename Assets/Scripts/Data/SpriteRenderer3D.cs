using UnityEngine;
using System.IO;

public class SpriteRenderer3D : MonoBehaviour
{
    [SerializeField] private Camera renderCamera;
    [SerializeField] private RenderTexture renderTexture;
    [SerializeField] private string saveFolder = "Assets/Sprites/";

    public void Capture(string fileName)
    {
        // Gán render target
        renderCamera.targetTexture = renderTexture;
        RenderTexture.active = renderTexture;

        // Render ra RenderTexture
        renderCamera.Render();

        // Copy sang Texture2D
        Texture2D tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        tex.Apply();

        // Lưu ra PNG
        byte[] bytes = tex.EncodeToPNG();
        string path = Path.Combine(saveFolder, fileName + ".png");
        File.WriteAllBytes(path, bytes);

        // Reset
        renderCamera.targetTexture = null;
        RenderTexture.active = null;

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh(); // Refresh để thấy file ngay trong Editor
#endif

        Debug.Log("Saved sprite to: " + path);
    }
}
