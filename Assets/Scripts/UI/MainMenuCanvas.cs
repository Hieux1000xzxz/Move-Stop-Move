using UnityEngine;
using UnityEngine.UI;

public class MainMenuCanvas : BaseCanvas
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button shopButton;

    private void Awake()
    {
        playButton.onClick.AddListener(OnPlayClicked);
        shopButton.onClick.AddListener(OnShopClicked);
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
}
