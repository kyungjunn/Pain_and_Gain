using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct QuestRewardEntry
{
    public QuestReward reward;
    [Min(1)] public int weight;
}

[CreateAssetMenu(fileName = "QuestRewardPool", menuName = "Game/Quest Reward Pool")]
public sealed class QuestRewardPoolSO : ScriptableObject
{
    public List<QuestRewardEntry> entries = new List<QuestRewardEntry>();

    // 후보 하나는 확정 보상, 여러 후보는 가중치 추첨이다.
    public bool TrySelect(float sample, out QuestReward reward)
    {
        return TrySelect(sample, null, out reward);
    }

    public bool TrySelect(float sample, Func<QuestReward, bool> isEligible, out QuestReward reward)
    {
        reward = default;
        if (entries == null || float.IsNaN(sample) || sample < 0f || sample > 1f)
            return false;

        double totalWeight = 0;
        foreach (QuestRewardEntry entry in entries)
        {
            if (IsSelectable(entry, isEligible))
                totalWeight += entry.weight;
        }
        if (totalWeight <= 0) return false;

        double remaining = sample * totalWeight;
        foreach (QuestRewardEntry entry in entries)
        {
            if (!IsSelectable(entry, isEligible)) continue;
            reward = entry.reward;
            remaining -= entry.weight;
            if (remaining < 0) return true;
        }
        // Random.value는 1을 반환할 수 있다.
        return true;
    }

    private static bool IsSelectable(QuestRewardEntry entry, Func<QuestReward, bool> isEligible)
    {
        return entry.weight > 0 && entry.reward.IsValid && (isEligible == null || isEligible(entry.reward));
    }
}
