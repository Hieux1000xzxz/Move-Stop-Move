using System;
using UnityEngine;
using UnityEngine.UI;

public class AvatarItem : MonoBehaviour
{
    [Header("References")] [SerializeField]
    private Image avatarImage;

    [SerializeField] private Button button;
    [SerializeField] private GameObject chooseHighlight;

    private int avatarIndex;
    private Action<int> onAvatarSelected;

    public void Initialize(int index, Sprite avatarSprite, Action<int> onSelectedCallback)
    {
        avatarIndex = index;
        onAvatarSelected = onSelectedCallback;

        if (avatarImage != null && avatarSprite != null)
        {
            avatarImage.sprite = avatarSprite;
            avatarImage.preserveAspect = true;
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }

        SetHighlight(false);
    }

    private void OnClick()
    {
        onAvatarSelected?.Invoke(avatarIndex);
    }

    public void SetHighlight(bool active)
    {
        if (chooseHighlight != null)
            chooseHighlight.SetActive(active);
    }
}