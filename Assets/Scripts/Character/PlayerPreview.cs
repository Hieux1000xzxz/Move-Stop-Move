using UnityEngine;
using TMPro;

public class PlayerPreview : MonoBehaviour
{
    [Header("Weapon Preview")] [SerializeField]
    private Transform weaponSpawnPoint;

    [SerializeField] private Vector3 weaponRotationOffset;
    [SerializeField] private Vector3 weaponPositionOffset;

    [Header("Player Name Display")] [SerializeField]
    private TextMeshPro playerNameText;

    [SerializeField] private Vector3 nameOffset = new Vector3(0, 0.8f, 0);
    [SerializeField] private Color nameColor = Color.yellow;
    [SerializeField] private int nameFontSize = 4;
    [SerializeField] private bool faceCamera = true;

    private GameObject currentWeaponObj;
    private Camera cam;
    private string currentPlayerName = "";

    private void Start()
    {
        cam = Camera.main;

        currentPlayerName = PlayerPrefs.GetString("PlayerName", "Player");

        UpdateNameText();
        UpdateNamePosition();
    }

    private void LateUpdate()
    {
        if (playerNameText == null) return;

        UpdateNamePosition();

        if (faceCamera && cam != null)
            playerNameText.transform.rotation = Quaternion.LookRotation(cam.transform.forward);
    }

    public void ShowWeapon(WeaponData weaponData)
    {
        if (currentWeaponObj != null)
        {
            Destroy(currentWeaponObj);
            currentWeaponObj = null;
        }

        if (weaponData != null && weaponData.weaponPrefab != null)
        {
            currentWeaponObj = Instantiate(
                weaponData.weaponPrefab,
                weaponSpawnPoint.position + weaponPositionOffset,
                weaponSpawnPoint.rotation * Quaternion.Euler(weaponRotationOffset),
                weaponSpawnPoint
            );
        }
    }

    private void UpdateNameText()
    {
        if (playerNameText == null) return;

        playerNameText.text = currentPlayerName;
        playerNameText.color = nameColor;
        playerNameText.fontSize = nameFontSize;
    }

    private void UpdateNamePosition()
    {
        if (playerNameText == null) return;
        playerNameText.transform.localPosition = nameOffset;
    }

    public void SetPlayerName(string playerName)
    {
        currentPlayerName = playerName;
        UpdateNameText();
    }
}