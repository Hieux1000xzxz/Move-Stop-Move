using UnityEngine;
using UnityEngine.UI;

public class MusicToggleButton : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite musicOnSprite;
    [SerializeField] private Sprite musicOffSprite;

    [Header("Refs")]
    [SerializeField] private Image buttonImage;
    [SerializeField] private Button button;

    private bool isMusicOn = true;

    private void Start()
    {
        if (button == null) button = GetComponent<Button>();
        if (buttonImage == null) buttonImage = GetComponent<Image>();

        button.onClick.AddListener(ToggleMusic);

        UpdateButtonSprite();
    }

    private void ToggleMusic()
    {
        isMusicOn = !isMusicOn;

        if (isMusicOn)
        {
            SoundManager.Instance.PlayBGM();
        }
        else
        {
            SoundManager.Instance.StopBGM();
        }

        UpdateButtonSprite();
    }

    private void UpdateButtonSprite()
    {
        if (buttonImage != null)
        {
            buttonImage.sprite = isMusicOn ? musicOnSprite : musicOffSprite;
        }
    }
}