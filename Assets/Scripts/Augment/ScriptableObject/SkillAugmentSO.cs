using UnityEngine;

// 스킬 증강. 최초 획득 시 Resources 경로의 프리팹을 생성하고 이후 획득은 같은 런타임 스킬에 중첩된다.
[CreateAssetMenu(fileName = "New Skill Augment",
                 menuName = "Game/Skill Augment")]
public class SkillAugmentSO : AugmentSO
{
    [Header("Skill")]
    [SerializeField] private string skillResourcePath;
    [Min(0)] public int maxStacks = 1;

    public string SkillResourcePath => NormalizedResourcePath();

    public override string GetDisplayName() => augmentName;

    public string NormalizedResourcePath()
    {
        if (string.IsNullOrWhiteSpace(skillResourcePath))
        {
            return string.Empty;
        }

        return skillResourcePath.Trim().Replace('\\', '/').Replace(".prefab", string.Empty);
    }

    private void OnValidate()
    {
        maxStacks = Mathf.Max(0, maxStacks);
        skillResourcePath = NormalizedResourcePath();
    }
}
