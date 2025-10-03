using UnityEngine;
using UnityEngine.UI;

public class MainMenuCanvas : BaseCanvas
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button MultiplayerButton;
    private void Awake()
    {
        playButton.onClick.AddListener(OnPlayClicked);
        shopButton.onClick.AddListener(OnShopClicked);
        MultiplayerButton.onClick.AddListener(OnMultiplayerClicked);
    }

    private void OnPlayClicked()
    {
        Debug.Log("Play game!");
        UIManager.Instance.StartGame();
    }

    private void OnShopClicked()
    {
        Debug.Log("Open Shop!");
        UIManager.Instance.OpenShop();
    }

    private void OnMultiplayerClicked()
    {
        Debug.Log("Open Multi!");
        GameManager.Instance.HidePlayerPreview();
        UIManager.Instance.OpenConnection();
    }
}
