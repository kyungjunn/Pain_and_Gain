using System;
using UnityEngine;

public enum ItemType
{ 
    Equipment,
    Consumable
}

[CreateAssetMenu(fileName = "New Item Data", menuName = "Game/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Info")]
    [SerializeField] private string itemName;
    [SerializeField] private string itemDescription;
    [SerializeField] private ItemType itemType;
    [SerializeField] private string rarity;
    [SerializeField] private Sprite itemIcon;
    [SerializeField] private GameObject itemPrefab;

    [Header("Stats")]
    [SerializeField] private float bonusAttackDamage;
    [SerializeField] private float hpRecoveryAmount;

    public string ItemName => itemName;
    public string ItemDescription => itemDescription;
    public ItemType ItemType => itemType;
    public string Rarity => rarity;
    public Sprite ItemIcon => itemIcon;
    public GameObject ItemPrefab => itemPrefab;
    public float BonusAttackDamage => bonusAttackDamage;
    public float HpRecoveryAmount => hpRecoveryAmount;

}
