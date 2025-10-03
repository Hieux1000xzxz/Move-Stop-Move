using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_InputField))]
public class MobileInputFilter : MonoBehaviour
{
    [SerializeField]private TMP_InputField inputField;

    void Awake()
    {
        inputField.keyboardType = TouchScreenKeyboardType.Default;
        inputField.contentType = TMP_InputField.ContentType.Custom;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.characterValidation = TMP_InputField.CharacterValidation.None;

        inputField.caretBlinkRate = 0.65f;
        inputField.caretWidth = 2;

        inputField.onValueChanged.AddListener(FilterInput);
    }

    private void FilterInput(string text)
    {
        int caretPos = inputField.stringPosition;

        string filtered = "";
        foreach (char c in text)
        {
            if ((c >= 'A' && c <= 'Z') ||
                (c >= 'a' && c <= 'z') ||
                (c >= '0' && c <= '9')
             )
            {
                filtered += c;
            }
        }
        if (filtered != text)
        {
            inputField.text = filtered;

            inputField.stringPosition = Mathf.Clamp(caretPos, 0, filtered.Length);
            inputField.caretPosition = inputField.stringPosition;
            inputField.selectionAnchorPosition = inputField.stringPosition;
            inputField.selectionFocusPosition = inputField.stringPosition;
        }
    }
}
