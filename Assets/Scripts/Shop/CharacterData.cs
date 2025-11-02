using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "CharacterData", menuName = "Game/Character Data")]
public class CharacterData : ScriptableObject, IShopItemData
{
    [FormerlySerializedAs("characterName")] 
    [SerializeField] private string characterName;
    [SerializeField] private Sprite icon;
    [SerializeField] private int price;
    [SerializeField] private string description;
    [SerializeField] private GameObject previewPrefab;

    [Header("Stats")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private int health = 100;
    [SerializeField] private int attack = 10;

    [Header("Prefab")]
    [SerializeField] private GameObject prefab;

    
    [Header("Socket")]
    [SerializeField] private Transform handSocket;
    public Transform HandSocket => handSocket;

    // ──────────────── Properties ────────────────
    public string Name => characterName;
    public Sprite Icon => icon;
    public int Price => price;
    public string Description => description;
    public GameObject PreviewPrefab => previewPrefab;

    public float MoveSpeed => moveSpeed;
    public int Health => health;
    public int Attack => attack;

    public GameObject Prefab => prefab;
}