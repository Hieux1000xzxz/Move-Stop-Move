using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GamePlayCanvas : BaseCanvas
{
    [SerializeField] GameObject gameOverUI;
    [SerializeField] GameObject gameWinUI;
    [SerializeField] private Button backToMenuButton;
    [SerializeField] private Button backToMenu1Button;

    private void Awake()
    {
        gameOverUI.SetActive(false);
    }
    private void Start()
    {
        backToMenuButton.onClick.AddListener(OnBackToMenu);
        backToMenu1Button.onClick.AddListener(OnBackToMenu);

    }

    private void Update()
    {
        
    }

    private void OnBackToMenu()
    {
        Debug.Log("Back to Main Menu");
        SceneManager.LoadScene("Level");
    }

    public void OnGameOver()
    {
        gameOverUI.SetActive(true);
    }

    public void OnGameWin()
    {
        gameWinUI.SetActive(true);
    }
}
