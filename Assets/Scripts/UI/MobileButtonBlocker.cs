using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileButtonBlocker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    private static bool isAnyButtonPressed = false;
    private bool isThisButtonPressed = false;

    [SerializeField] private float tapCooldown = 0.4f;
    private float lastTapTime = -1f;

    [SerializeField] private Button button;
    public void OnPointerDown(PointerEventData eventData)
    {
        if (isAnyButtonPressed && !isThisButtonPressed)
        {
            eventData.Use();
            return;
        }

        if (Time.time - lastTapTime < tapCooldown)
        {
            eventData.Use();
            return;
        }

        isAnyButtonPressed = true;
        isThisButtonPressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isAnyButtonPressed = false;
        isThisButtonPressed = false;
        lastTapTime = Time.time;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"{gameObject.name} clicked!");
    }
}
