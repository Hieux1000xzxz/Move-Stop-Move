using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GamePlayCanvas : BaseCanvas
{
    [SerializeField] GameObject gameOverUI;
    [SerializeField] GameObject gameWinUI;
    [SerializeField] GameObject menuUI;
    [SerializeField] private Button menuButton;
    [SerializeField] private Button backToMenuButton;
    [SerializeField] private Button backToMenu1Button;
    [SerializeField] private Button exitGameButton;

    private ConnectionCanvas connectionCanvas;
    private string lobbyId;
    private string playerName;

    public void Init(ConnectionCanvas connection, string lobbyId, string playerName)
    {
        this.connectionCanvas = connection;
        this.lobbyId = lobbyId;
        this.playerName = playerName;
    }

    private void Awake()
    {
        gameOverUI.SetActive(false);
    }
    private void Start()
    {
        backToMenuButton.onClick.AddListener(OnBackToMenu);
        backToMenu1Button.onClick.AddListener(OnBackToMenu);
        menuButton.onClick.AddListener(OnMenuOpen);
        exitGameButton.onClick.AddListener(OnExitGame);
    }
    private void OnExitGame()
    {
        Debug.Log("Exit Match");

        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.IsHost)
        {
            // Host: shutdown toàn bộ
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
        }
        else if (NetworkManager.Singleton.IsClient)
        {

            // Sau đó tự disconnect local
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
        }

        if (connectionCanvas != null)
        {
            connectionCanvas.HandleExitLogic();
        }

        GameManager.Instance.ShowMainMenu();
        SceneManager.LoadScene("Level");
    }

    private void OnMenuOpen()
    {
        menuUI.SetActive(true);
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
