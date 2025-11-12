using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MainMenuCanvas : BaseCanvas
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button shopButton;
    [SerializeField] private Button MultiplayerButton;

    [Header("Player Preview List")] [SerializeField]
    private List<PlayerPreview> playerPreviews = new List<PlayerPreview>();

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
        HideAllPlayerPreviews();
    }

    private void OnMultiplayerClicked()
    {
        Debug.Log("Open Multi!");
        GameManager.Instance.HidePlayerPreview();
        UIManager.Instance.OpenConnection();
    }

    private void HideAllPlayerPreviews()
    {
        foreach (var preview in playerPreviews)
        {
            if (preview != null)
                preview.gameObject.SetActive(false);
        }
    }

    public void ShowAllPlayerPreviews()
    {
        foreach (var preview in playerPreviews)
        {
            if (preview != null)
                preview.gameObject.SetActive(true);
        }
    }
}