using System;
using UnityEngine;

public enum QuestRewardType
{
    Experience,
    Heal,
    UltimateCharge,
    AugmentChoice
}

// 모든 수치는 고정량이다. 체력/궁극기는 각 시스템의 최대치로 제한한다.
[Serializable]
public struct QuestReward
{
    public QuestRewardType type;
    [Min(1)] public int amount;

    public bool IsValid => amount > 0 && Enum.IsDefined(typeof(QuestRewardType), type);

    public string Description
    {
        get
        {
            switch (type)
            {
                case QuestRewardType.Experience: return $"경험치 {amount}";
                case QuestRewardType.Heal: return $"체력 {amount} 회복";
                case QuestRewardType.UltimateCharge: return $"궁극기 게이지 {amount} 충전";
                case QuestRewardType.AugmentChoice: return $"증강 선택 {amount}회";
                default: return "유효하지 않은 보상";
            }
        }
    }
}

public struct QuestRewardResult
{
    public QuestReward reward;
    public bool applied;
    public float grantedAmount;

    public string Description
    {
        get
        {
            if (!applied) return "보상 지급 실패";
            switch (reward.type)
            {
                case QuestRewardType.Heal: return $"체력 {grantedAmount:0.#} 회복";
                case QuestRewardType.UltimateCharge: return $"궁극기 게이지 {grantedAmount:0.#} 충전";
                default: return reward.Description;
            }
        }
    }
}
