using UnityEngine;

[CreateAssetMenu(fileName = "New Augment",
                 menuName = "Game/Augment")]
public class StatAugmentSO : ScriptableObject
{
    [Header("Info")]
    public string augmentName;
    [TextArea]
    public string description;

    public Sprite icon;

    [Header("Stat")]
    public AugmentType type;
    public float value;
}   