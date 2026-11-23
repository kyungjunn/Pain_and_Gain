using System;
using System.Collections.Generic;
using UnityEngine;

public enum AugmentRarity { Normal, Legendary }

// 결과 화면에 표시할 증강 1개
[Serializable]
public class AugmentData
{
    public Sprite icon;
    public string valueText;      // 예: "+20", "+1000"
    public string displayName;    // 예: "Move Speed", "Legendary Damage"
    public AugmentRarity rarity;
}

// 결과 화면에 넘겨줄 데이터 묶음 (게임 매니저에서 채워서 전달)
[Serializable]
public class ResultData
{
    public float survivalTime;    // 초 단위
    public int monsterKills;
    public int finalLevel;

    public float attackDamage;
    public float hp;
    public float maxHp;
    public float speed;
    public float attackSpeed;
    public float defense;

    public List<AugmentData> augments = new List<AugmentData>();

    // 세션 정리 전에 값만 복사한다. 런타임 증강 인스턴스는 보관하지 않는다.
    public static ResultData Capture(PlayerStats stats, PlayerLevelSystem levels,
        PlayerAugments skills, float elapsed, int kills)
    {
        var result = new ResultData
        {
            survivalTime = Mathf.Max(0f, elapsed),
            monsterKills = kills,
            finalLevel = levels.level,
            attackDamage = stats.AttackDamage,
            hp = 0f,
            maxHp = stats.HP,
            speed = stats.MoveSpeed,
            attackSpeed = stats.AttackSpeed,
            defense = stats.Defense
        };
        if (skills != null)
            foreach (SkillAugmentSO skill in skills.OwnedSkills)
                result.augments.Add(new AugmentData
                {
                    icon = skill.icon,
                    displayName = skill.augmentName,
                    valueText = $"x{skills.GetSkillStackCount(skill)}",
                    rarity = skill.rarity == RarityType.Legendary
                        ? AugmentRarity.Legendary : AugmentRarity.Normal
                });

        // 스탯은 카드 획득 횟수가 아니라 페널티 후 남은 실제 보너스를 표시한다.
        foreach (AugmentType type in Enum.GetValues(typeof(AugmentType)))
        {
            float bonus = stats.GetAugmentBonus(type);
            if (bonus <= 0f) continue;
            result.augments.Add(new AugmentData
            {
                displayName = type.ToString(),
                valueText = $"+{bonus:0.##}",
                rarity = AugmentRarity.Normal
            });
        }
        return result;
    }
}
