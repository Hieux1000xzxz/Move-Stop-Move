using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GamePlayCanvas : BaseCanvas
{
    [SerializeField] GameObject gameOverUI;
    [SerializeField] GameObject gameWinUI;
    [SerializeField] GameObject menuUI;
    [SerializeField] GameObject viewUI;
    [SerializeField] private Button menuButton;
    [SerializeField] private Button backToMenuButton;
    [SerializeField] private Button backToMenuWinButton;
    [SerializeField] private Button exitGameButton;
    [SerializeField] private Button continueGameButton;
    [SerializeField] private Button continueViewGameButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    private ConnectionCanvas connectionCanvas;
    private string lobbyId;
    private string playerName;
    private float winExitTimer = -1f;
    private float winExitDelay = 5f;

    public void Init(ConnectionCanvas connection, string lobbyId, string playerName)
    {
        this.connectionCanvas = connection;
        this.lobbyId = lobbyId;
        this.playerName = playerName;
    }

    private void Awake()
    {
        gameOverUI.SetActive(false);
        gameWinUI.SetActive(false);
        menuUI.SetActive(false);
        viewUI.SetActive(false);
    }
    private void Update()
    {
        if (winExitTimer > 0)
        {
            winExitTimer -= Time.deltaTime;
            if (winExitTimer <= 0)
            {
                OnExitGame();
                winExitTimer = -1f;
            }
        }
    }

    private void Start()
    {
        backToMenuButton.onClick.AddListener(OnExitGame);
        backToMenuWinButton.onClick.AddListener(OnBackToMenu);
        menuButton.onClick.AddListener(OnMenuOpen);
        exitGameButton.onClick.AddListener(OnExitGame);
        continueGameButton.onClick.AddListener(() => menuUI.SetActive(false));
        continueViewGameButton.onClick.AddListener(OnContinueView);
        previousButton.onClick.AddListener(() => GameManager.Instance.RequestPreviousSpectatorTargetServerRpc());
        nextButton.onClick.AddListener(() => GameManager.Instance.RequestNextSpectatorTargetServerRpc());
    }

    public void UpdateExitButtonState(LobbyInfo lobby)
    {
        if (lobby == null) return;

        int playerCount = lobby.users != null ? lobby.users.Count : 0;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            exitGameButton.interactable = (playerCount <= 1);
            backToMenuButton.interactable = (playerCount <= 1);
        }
        else
        {
            exitGameButton.interactable = true;
            backToMenuButton.interactable = true;
        }
    }

    private void OnExitGame()
    {
        Debug.Log("Exit Match");

        if (NetworkManager.Singleton == null) 
            
            return;

        if (NetworkManager.Singleton.IsHost)
        {
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
        }
        else if (NetworkManager.Singleton.IsClient)
        {

            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
        }

        if (connectionCanvas != null)
        {
            connectionCanvas.HandleExitLogic();
        }
        UIManager.Instance.OpenLoadingCanvas();
        Invoke(nameof(OnBackToMenu), 2.4f);
    }

    private void OnContinueView()
    {
        GameManager.Instance.EnableSpectatorMode();
        viewUI.SetActive(true);
        gameOverUI.SetActive(false);
        menuButton.gameObject.SetActive(true);
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
        menuButton.gameObject.SetActive(false);
        gameOverUI.SetActive(true);
    }

    public void OnGameWin()
    {
        gameWinUI.SetActive(true);
        gameOverUI.SetActive(false);
        winExitTimer = winExitDelay;
    }
}
