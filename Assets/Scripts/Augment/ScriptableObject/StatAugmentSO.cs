using UnityEngine;

[CreateAssetMenu(fileName = "New Augment",
                 menuName = "Game/Augment")]
public class StatAugmentSO : ScriptableObject
{
    [Header("Info")] // 기본 정보
    public string augmentName;
    [TextArea]
    public string description;

    public Sprite icon;

    [Header("Stat")] // 스탯
    public AugmentType type;
    public float value;

    [Header("Rarity")] // 희귀도
    public RarityType rarity;

    [Range(1, 100)] // 확률
    public int weight = 50; // 높을 수록 잘 나옴.
}   