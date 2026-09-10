using UnityEngine;

// 스킬 증강. 최초 획득 시 프리팹을 생성하고 이후 획득은 같은 런타임 스킬에 중첩된다.
[CreateAssetMenu(fileName = "New Skill Augment",
                 menuName = "Game/Skill Augment")]
public class SkillAugmentSO : AugmentSO
{
    [Header("Skill")] // 스킬
    public AugmentSkill skillPrefab;
    [Min(0)] public int maxStacks = 1;

    public override string GetDisplayName() => augmentName;

    private void OnValidate()
    {
        maxStacks = Mathf.Max(0, maxStacks);
    }
}
