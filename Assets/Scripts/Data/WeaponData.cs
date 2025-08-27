using UnityEngine;

[CreateAssetMenu(fileName = "WeaponData", menuName = "Game/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public WeaponType weaponType;
    public string weaponName;
    public Sprite weaponIcon;
    public int price;
    public string description;

    [Header("Stats")]
    public float speed = 12f;
    public int damage = 1;
}
