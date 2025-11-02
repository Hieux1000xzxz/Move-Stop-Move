using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class CharacterItem : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private GameObject chooseHighlight;
    [SerializeField] private GameObject boughtOverlay;
    [SerializeField] private GameObject selectedHighlight;
    [SerializeField] private Button button;

    public CharacterData Data { get; private set; }
    public event Action<CharacterData> OnClicked;

    public void Initialize(CharacterData data, bool isBought, bool isSelected)
    {
        Data = data;
        icon.sprite = data.Icon;
        nameText.text = data.Name;
        priceText.text = data.Price.ToString();

        SetBought(isBought);
        SetSelected(isSelected);
        SetChosen(false);
        button.onClick.AddListener(() => OnClicked?.Invoke(Data));
    }

    public void SetBought(bool value) => boughtOverlay.SetActive(value);
    public void SetSelected(bool value) => selectedHighlight.SetActive(value);
    
    public void SetChosen(bool isChosen)
    {
        chooseHighlight.SetActive(isChosen);
    }
    private void OnDestroy() => button.onClick.RemoveAllListeners();
}