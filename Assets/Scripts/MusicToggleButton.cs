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

    private bool isMusicOn;

    private const string MusicKey = "MusicOn";

    private void Start()
    {
        isMusicOn = PlayerPrefs.GetInt(MusicKey, 1) == 1;

        button.onClick.AddListener(ToggleMusic);

        ApplyMusicState();
    }

    private void ToggleMusic()
    {
        isMusicOn = !isMusicOn;

        // Save state for all scenes
        PlayerPrefs.SetInt(MusicKey, isMusicOn ? 1 : 0);
        PlayerPrefs.Save();

        ApplyMusicState();
    }

    private void ApplyMusicState()
    {
        if (isMusicOn)
        {
            SoundManager.Instance.PlayBGM();
        }
        else
        {
            SoundManager.Instance.StopBGM();
        }

        if (buttonImage != null)
        {
            buttonImage.sprite = isMusicOn ? musicOnSprite : musicOffSprite;
        }
    }
}