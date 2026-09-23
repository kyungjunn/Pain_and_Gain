using UnityEngine;

public enum PlayerCombatAugmentEffect
{
    BasicProjectileCount,
    SkillDamage,
    SkillRange,
    SkillDuration
}

// 플레이어 전투 동작을 바꾸는 공용 증강 데이터.
[CreateAssetMenu(fileName = "New Player Combat Augment",
                 menuName = "Game/Player Combat Augment")]
public sealed class PlayerCombatAugmentSO : SkillAugmentSO
{
    [Header("Target")]
    [SerializeField] private PlayerCharacterId requiredCharacter = PlayerCharacterId.Ninja;
    [SerializeField] private PlayerCombatAugmentEffect effect;
    [SerializeField] private PlayerSkillSO targetSkill;

    [Header("Value")]
    [SerializeField, Min(0f)] private float value = 1f;
    [SerializeField, Min(0f)] private float spreadDegrees = 20f;

    public PlayerCharacterId RequiredCharacter => requiredCharacter;
    public PlayerCombatAugmentEffect Effect => effect;
    public PlayerSkillSO TargetSkill => targetSkill;
    public float Value => value;
    public float SpreadDegrees => spreadDegrees;

    public override bool IsAvailableFor(GameObject player)
    {
        if (player == null)
        {
            return false;
        }

        PlayerCharacter character = player.GetComponent<PlayerCharacter>();
        if (character == null || !character.Is(requiredCharacter))
        {
            return false;
        }

        if (effect != PlayerCombatAugmentEffect.BasicProjectileCount && targetSkill == null)
        {
            return false;
        }

        if (targetSkill != null)
        {
            PlayerSkillController skillController = player.GetComponent<PlayerSkillController>();
            if (skillController == null || !skillController.HasSkill(targetSkill))
            {
                return false;
            }
        }

        if (effect == PlayerCombatAugmentEffect.BasicProjectileCount &&
            player.GetComponent<ProjectilePlayerAttack>() == null)
        {
            return false;
        }

        return true;
    }

    public override string GetDisplayName()
    {
        return augmentName;
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        value = Mathf.Max(0f, value);
        spreadDegrees = Mathf.Clamp(spreadDegrees, 0f, 360f);
    }
}
