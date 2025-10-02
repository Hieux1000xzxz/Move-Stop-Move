using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

public class GamePlayCanvas : BaseCanvas
{
    [SerializeField] private GameObject gameOverUI;
    [SerializeField] private GameObject gameWinUI;
    [SerializeField] private GameObject menuUI;
    [SerializeField] private GameObject viewUI;
    [SerializeField] private GameObject winnerUI;
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
                OnExitGame(false);
                winExitTimer = -1f;
            }
        }
    }

    private void Start()
    {
        backToMenuButton.onClick.AddListener(() => OnExitConfirm());
        backToMenuWinButton.onClick.AddListener(OnBackToMenu);
        menuButton.onClick.AddListener(OnMenuOpen);
        exitGameButton.onClick.AddListener(() => OnExitConfirm());
        continueGameButton.onClick.AddListener(() => menuUI.SetActive(false));
        continueViewGameButton.onClick.AddListener(OnContinueView);
        previousButton.onClick.AddListener(() => GameManager.Instance.RequestPreviousSpectatorTargetServerRpc());
        nextButton.onClick.AddListener(() => GameManager.Instance.RequestNextSpectatorTargetServerRpc());
    }

    public void UpdateExitButtonState(RelayLobbyInfo lobby)
    {
        if (lobby == null) return;

        int playerCount = lobby.users != null ? lobby.users.Count : 0;

        //if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        //{
        //    exitGameButton.interactable = (playerCount <= 1);
        //    backToMenuButton.interactable = (playerCount <= 1);
        //}
        //else
        //{
        //    exitGameButton.interactable = true;
        //    backToMenuButton.interactable = true;
        //}
    }
    private void OnExitConfirm()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
        {
            var notify = UIManager.Instance?.BindNotification();
            if (notify != null)
            {
                notify.SetText("If you leave, the game ends for all players. Continue?");
                notify.HideCloseButton();
                notify.ShowConfirmButton();
                notify.SetCallback((isConfirm) =>
                {
                    if (isConfirm)
                    {
                        OnExitGame(false);
                    }
                    else
                    {
                    }
                });
            }
        }
        else
        {
            OnExitGame(false);
        }
    }
    public void OnExitGame(bool showHostLeftMessage = false)
    {
        Debug.Log($"Exit Match - showHostLeftMessage: {showHostLeftMessage}");

        if (showHostLeftMessage &&
            NetworkManager.Singleton != null &&
            !NetworkManager.Singleton.IsHost)
        {
            UIManager.Instance?.SendNotification("Host has left the room. Returning to lobby...", 1);
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
        }

        if (GameManager.Instance != null)
        {
            Destroy(GameManager.Instance.gameObject);
        }

        if (UIManager.Instance != null)
        {
            Destroy(UIManager.Instance.gameObject);
        }

        if (connectionCanvas != null)
        {
            connectionCanvas.HandleExitLogic();
        }

        UIManager.Instance?.OpenLoadingCanvas();
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
        UIManager.Instance.HideCountText();
        gameOverUI.SetActive(true);
    }

    public void OnGameWin()
    {
        gameWinUI.SetActive(true);
        gameOverUI.SetActive(false);
        menuButton.gameObject.SetActive(false);
        winExitTimer = winExitDelay;
    }

    public void OnWinner()
    {
        winnerUI.SetActive(true);
        gameWinUI.SetActive(false);
        gameOverUI.SetActive(false);
        menuButton.gameObject.SetActive(false);
        winExitTimer = winExitDelay;
    }
}

