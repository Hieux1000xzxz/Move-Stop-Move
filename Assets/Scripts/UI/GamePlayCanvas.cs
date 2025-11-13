using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections;

public class GamePlayCanvas : BaseCanvas
{
    [Header("UI Panels")] [SerializeField] private GameObject gameOverUI;
    [SerializeField] private GameObject gameWinUI;
    [SerializeField] private GameObject menuUI;
    [SerializeField] private GameObject viewUI;
    [SerializeField] private GameObject winnerUI;

    [Header("Buttons")] [SerializeField] private Button menuButton;
    [SerializeField] private Button backToMenuButton;
    [SerializeField] private Button exitGameButton;
    [SerializeField] private Button continueGameButton;
    [SerializeField] private Button continueViewGameButton;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    [Header("Text Elements")] [SerializeField]
    private TextMeshProUGUI countDown;

    [SerializeField] private TextMeshProUGUI totalCoinText;
    [SerializeField] private TMP_Text earnedCoinText;

    [Header("Settings")] [SerializeField] private float winExitDelay = 5f;

    private ConnectionCanvas connectionCanvas;
    private string lobbyId;
    private string playerName;
    private float winExitTimer = -1f;

    #region Initialization

    public void Init(ConnectionCanvas connection, string lobbyId, string playerName)
    {
        this.connectionCanvas = connection;
        this.lobbyId = lobbyId;
        this.playerName = playerName;
    }

    private void Awake()
    {
        InitializeUI();
    }

    private void Start()
    {
        RegisterButtonListeners();
    }

    private void InitializeUI()
    {
        gameOverUI.SetActive(false);
        gameWinUI.SetActive(false);
        menuUI.SetActive(false);
        viewUI.SetActive(false);
        countDown.gameObject.SetActive(false);
        totalCoinText.gameObject.SetActive(false);
    }

    private void RegisterButtonListeners()
    {
        backToMenuButton.onClick.AddListener(OnExitConfirm);
        menuButton.onClick.AddListener(OnMenuOpen);
        exitGameButton.onClick.AddListener(OnExitConfirm);
        continueGameButton.onClick.AddListener(OnContinueGame);
        continueViewGameButton.onClick.AddListener(OnContinueView);
        previousButton.onClick.AddListener(OnPreviousSpectatorTarget);
        nextButton.onClick.AddListener(OnNextSpectatorTarget);
    }

    #endregion

    #region Update Loop

    private void Update()
    {
        HandleWinExitTimer();
    }

    private void HandleWinExitTimer()
    {
        if (winExitTimer > 0)
        {
            UpdateWinExitTimer();
        }
        else
        {
            HideCountDown();
        }
    }

    private void UpdateWinExitTimer()
    {
        winExitTimer -= Time.deltaTime;
        ShowCountDown();

        if (winExitTimer <= 0)
        {
            HandleTimerExpired();
        }
    }

    private void ShowCountDown()
    {
        if (countDown == null) return;

        countDown.gameObject.SetActive(true);
        int secondsLeft = Mathf.CeilToInt(winExitTimer);
        countDown.text = $"Returning to menu in {secondsLeft}s...";
    }

    private void HideCountDown()
    {
        if (countDown != null && countDown.gameObject.activeSelf)
        {
            countDown.gameObject.SetActive(false);
        }
    }

    private void HandleTimerExpired()
    {
        winExitTimer = -1f;
        OnExitGame(false);
    }

    #endregion

    #region Button Callbacks

    private void OnContinueGame()
    {
        ShowMenuButton();
        HideMenuUI();
    }

    private void OnMenuOpen()
    {
        ShowMenuUI();
        HideMenuButton();
    }

    private void OnPreviousSpectatorTarget()
    {
        GameManager.Instance.RequestPreviousSpectatorTargetServerRpc();
    }

    private void OnNextSpectatorTarget()
    {
        GameManager.Instance.RequestNextSpectatorTargetServerRpc();
    }

    private void OnContinueView()
    {
        EnableSpectatorView();
        ShowGameUI();
        StartCoroutine(RequestCoinAfterFocus());
    }

    #endregion

    #region Exit Game Logic

    private void OnExitConfirm()
    {
        if (IsHostInMultiplayer())
        {
            ShowHostExitConfirmation();
        }
        else
        {
            OnExitGame(false);
        }
    }

    private bool IsHostInMultiplayer()
    {
        return NetworkManager.Singleton != null &&
               NetworkManager.Singleton.IsHost &&
               !connectionCanvas.isSinglePlayerMode;
    }

    private void ShowHostExitConfirmation()
    {
        var notify = UIManager.Instance.BindNotification();
        if (notify == null) return;

        notify.SetText("If you leave, the game ends for all players. Continue?");
        notify.HideCloseButton();
        notify.ShowConfirmButton();
        notify.ShowMainPanel();
        notify.SetCallback(OnHostExitConfirmed);
    }

    private void OnHostExitConfirmed(bool isConfirm)
    {
        if (isConfirm)
        {
            OnExitGame(false);
        }
    }

    public void OnExitGame(bool showHostLeftMessage = false)
    {
        HandleHostLeftMessage(showHostLeftMessage);
        ShutdownNetworking();
        CleanupGameObjects();
        HandleConnectionCanvasExit();
        LoadMenuScene();
    }

    private void HandleHostLeftMessage(bool showHostLeftMessage)
    {
        if (showHostLeftMessage && IsClientInNetwork())
        {
            UIManager.Instance.SendNotification(
                "Host has left the room. Returning to the menu scene...", 2);
        }
    }

    private bool IsClientInNetwork()
    {
        return NetworkManager.Singleton != null && !NetworkManager.Singleton.IsHost;
    }

    private void ShutdownNetworking()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            Destroy(NetworkManager.Singleton.gameObject);
        }
    }

    private void CleanupGameObjects()
    {
        DestroyGameManager();
        DestroyUIManager();
    }

    private void DestroyGameManager()
    {
        if (GameManager.Instance != null)
        {
            Destroy(GameManager.Instance.gameObject);
        }
    }

    private void DestroyUIManager()
    {
        if (UIManager.Instance != null)
        {
            Destroy(UIManager.Instance.gameObject);
        }
    }

    private void HandleConnectionCanvasExit()
    {
        if (connectionCanvas != null)
        {
            connectionCanvas.HandleExitLogic();
        }
    }

    private void LoadMenuScene()
    {
        UIManager.Instance.OpenLoadingCanvas();
        Invoke(nameof(OnBackToMenu), 2.4f);
    }

    private void OnBackToMenu()
    {
        Debug.Log("Back to Main Menu");
        SceneManager.LoadScene("Level");
    }

    #endregion

    #region Game End States

    public void OnGameOver()
    {
        SetupBaseEndUI();
        UIManager.Instance.HideCountText();
        gameOverUI.SetActive(true);
    }

    public void OnGameWin()
    {
        SetupBaseEndUI();
        ShowGameWinUI();
        StartWinExitTimer();
        CommitPlayerCoins();
    }

    public void OnWinner()
    {
        SetupBaseEndUI();
        ShowWinnerUI();
        StartWinExitTimer();
    }

    private void SetupBaseEndUI()
    {
        HideMenuButton();
        HideAllEndGamePanels();
        HideCoinDisplay();
        HandleEndMatchCoinText();
    }

    private void HideAllEndGamePanels()
    {
        gameOverUI.SetActive(false);
        gameWinUI.SetActive(false);
        winnerUI.SetActive(false);
    }

    private void ShowGameWinUI()
    {
        gameWinUI.SetActive(true);
    }

    private void ShowWinnerUI()
    {
        winnerUI.SetActive(true);
    }

    private void StartWinExitTimer()
    {
        winExitTimer = winExitDelay;
        countDown.gameObject.SetActive(true);
    }

    private void CommitPlayerCoins()
    {
        CoinManager.Instance.CommitSessionCoins();
    }

    private void HandleEndMatchCoinText()
    {
        if (ShouldShowEndMatchCoinText())
        {
            CoinManager.Instance.ShowEndMatchCoinText();
        }
        else
        {
            CoinManager.Instance.HideEndMatchCoinText();
        }
    }

    private bool ShouldShowEndMatchCoinText()
    {
        return !GameManager.Instance.IsSpectatorMode && !viewUI.activeSelf;
    }

    #endregion

    #region Spectator Mode

    private void EnableSpectatorView()
    {
        GameManager.Instance.EnableSpectatorMode();
        viewUI.SetActive(true);
        gameOverUI.SetActive(false);
    }

    private void ShowGameUI()
    {
        ShowMenuButton();
        CoinManager.Instance.ShowCoinText();
        UIManager.Instance.ShowCountText();
        totalCoinText.gameObject.SetActive(false);
    }

    private IEnumerator RequestCoinAfterFocus()
    {
        yield return new WaitForSeconds(0.2f);
    }

    #endregion

    #region UI Helper Methods

    private void ShowMenuButton()
    {
        menuButton.gameObject.SetActive(true);
    }

    private void HideMenuButton()
    {
        menuButton.gameObject.SetActive(false);
    }

    private void ShowMenuUI()
    {
        menuUI.SetActive(true);
    }

    private void HideMenuUI()
    {
        menuUI.SetActive(false);
    }

    private void HideCoinDisplay()
    {
        CoinManager.Instance.HideCoinText();
    }

    #endregion
}