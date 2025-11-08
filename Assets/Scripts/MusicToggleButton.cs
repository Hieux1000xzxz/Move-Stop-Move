using UnityEngine;
using UnityEngine.UI;

public class MusicToggleButton : MonoBehaviour
{
    [Header("Sprites")] [SerializeField] private Sprite musicOnSprite;
    [SerializeField] private Sprite musicOffSprite;

    [Header("Refs")] [SerializeField] private Image buttonImage;
    [SerializeField] private Button button;

    private bool isMusicOn;
    private const string MusicPrefKey = "MusicState";

    private void Start()
    {
        if (button == null) button = GetComponent<Button>();
        if (buttonImage == null) buttonImage = GetComponent<Image>();

        isMusicOn = PlayerPrefs.GetInt(MusicPrefKey, 1) == 1;
        ApplyMusicState();

        button.onClick.AddListener(ToggleMusic);
    }

    private void ToggleMusic()
    {
        isMusicOn = !isMusicOn;
        ApplyMusicState();

        PlayerPrefs.SetInt(MusicPrefKey, isMusicOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplyMusicState()
    {
        if (buttonImage != null)
            buttonImage.sprite = isMusicOn ? musicOnSprite : musicOffSprite;

        if (isMusicOn)
            SoundManager.Instance.PlayBGM();
        else
            SoundManager.Instance.StopBGM();
    }
}