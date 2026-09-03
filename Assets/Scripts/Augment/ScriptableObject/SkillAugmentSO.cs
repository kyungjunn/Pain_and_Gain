using UnityEngine;

// 스킬 증강. 획득 시 skillPrefab이 플레이어 밑에 생성되고, 박탈 시 통째로 Destroy된다.
[CreateAssetMenu(fileName = "New Skill Augment",
                 menuName = "Game/Skill Augment")]
public class SkillAugmentSO : AugmentSO
{
    [Header("Skill")] // 스킬
    public AugmentSkill skillPrefab;

    public override string GetDisplayName() => augmentName;
}
