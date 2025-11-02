using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class CharacterShopCanvas : BaseCanvas
{
    [Header("UI References")]
    [SerializeField] private Transform grid;
    [SerializeField] private GameObject characterItemPrefab;
    [SerializeField] private Button buyButton;
    [SerializeField] private Button selectButton;
    [SerializeField] private Button backButton;
    [SerializeField] private TextMeshProUGUI coinText;
    [SerializeField] private Transform previewPoint;
    [SerializeField] private CharacterData[] characters;

    private IShopService<CharacterData> shopService;
    private IPreviewStrategy<CharacterData> previewStrategy;

    private CharacterData selected;
    private readonly Dictionary<string, CharacterItem> items = new();

    private void Start()
    {
        shopService = ShopServiceFactory.CreateCharacterService();
        previewStrategy = new CharacterPreviewStrategy(previewPoint);

        buyButton.onClick.AddListener(() => shopService.BuyItem(selected));
        selectButton.onClick.AddListener(() => shopService.SelectItem(selected));
        backButton.onClick.AddListener(BackToMainMenu);

        InitGrid();
        LoadSelected();
    }

    private void InitGrid()
    {
        foreach (Transform c in grid)
            Destroy(c.gameObject);

        foreach (var data in characters)
        {
            var obj = Instantiate(characterItemPrefab, grid);
            var item = obj.GetComponent<CharacterItem>();

            bool isBought = shopService.IsBought(data);
            bool isSelected = shopService.IsSelected(data);

            item.Initialize(data, isBought, isSelected);
            item.OnClicked += OnItemSelected;

            items[data.Name] = item;
        }

        UpdateCoinUI();
    }

    private void OnItemSelected(CharacterData data)
    {
        selected = data;
        foreach (var i in items.Values)
            i.SetSelected(i.Data == data);

        previewStrategy.ShowPreview(data);
        UpdateButtons();
    }

    private void LoadSelected()
    {
        string saved = PlayerPrefs.GetString("SelectedCharacter", "");
        if (string.IsNullOrEmpty(saved)) return;

        if (items.TryGetValue(saved, out var item))
        {
            selected = item.Data;
            previewStrategy.ShowPreview(selected);
            UpdateButtons();
        }
    }

    private void UpdateButtons()
    {
        if (selected == null) return;

        bool bought = shopService.IsBought(selected);
        bool chosen = shopService.IsSelected(selected);

        buyButton.interactable = !bought;
        selectButton.interactable = bought && !chosen;
    }

    public void UpdateCoinUI()
    {
        coinText.text = $"Coins: {CoinManager.Instance.GetShopCoins()}";
    }

    private void BackToMainMenu()
    {
        Hide();
        UIManager.Instance.OpenMainMenu();
    }
}
