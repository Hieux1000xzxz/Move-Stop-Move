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
        // Ở đây bạn load scene gameplay
        // SceneManager.LoadScene("GameScene");
    }

    private void OnShopClicked()
    {
        Debug.Log("Open Shop!");
        UIManager.Instance.OpenShop();  // Gọi UIManager thay vì chỉ Hide()
    }

}
