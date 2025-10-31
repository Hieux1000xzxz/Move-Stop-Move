using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class KeepAspectFill : MonoBehaviour
{
    private Image img;
    private RectTransform rect;

    void Awake()
    {
        img = GetComponent<Image>();
        rect = GetComponent<RectTransform>();
    }

    void Update()
    {
        if (img.sprite == null) return;

        float imageRatio = (float)img.sprite.rect.width / img.sprite.rect.height;
        float screenRatio = (float)Screen.width / Screen.height;

        if (imageRatio > screenRatio)
        {
            rect.sizeDelta = new Vector2(Screen.height * imageRatio, Screen.height);
        }
        else
        {
            rect.sizeDelta = new Vector2(Screen.width, Screen.width / imageRatio);
        }
    }
}