using UnityEngine;

// 퀘스트 보상을 기존 플레이어/증강 API에 연결한다.
public static class QuestRewardProcessor
{
    public static bool CanApply(GameObject player, QuestReward reward)
    {
        if (player == null || !reward.IsValid) return false;
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null && health.IsDead) return false;

        switch (reward.type)
        {
            case QuestRewardType.Experience:
                return player.GetComponent<PlayerLevelSystem>() != null;
            case QuestRewardType.Heal:
                return health != null;
            case QuestRewardType.UltimateCharge:
                return player.GetComponent<PlayerUltimateGauge>() != null;
            case QuestRewardType.AugmentChoice:
                return UIManager.Instance != null
                    && UIManager.Instance.augmentPanelUI != null
                    && AugmentManager.Instance != null
                    && AugmentResourceLoader.Instance != null
                    && AugmentResourceLoader.Instance.IsActiveSession;
            default:
                return false;
        }
    }

    public static QuestRewardResult Apply(GameObject player, QuestReward reward)
    {
        QuestRewardResult result = new QuestRewardResult { reward = reward };
        if (!CanApply(player, reward)) return result;

        switch (reward.type)
        {
            case QuestRewardType.Experience:
                player.GetComponent<PlayerLevelSystem>().AddExp(reward.amount);
                result.grantedAmount = reward.amount;
                break;
            case QuestRewardType.Heal:
                result.grantedAmount = player.GetComponent<PlayerHealth>().Heal(reward.amount);
                break;
            case QuestRewardType.UltimateCharge:
                PlayerUltimateGauge gauge = player.GetComponent<PlayerUltimateGauge>();
                float previous = gauge.Current;
                gauge.Add(reward.amount);
                result.grantedAmount = gauge.Current - previous;
                break;
            case QuestRewardType.AugmentChoice:
                for (int i = 0; i < reward.amount; i++)
                    UIManager.Instance.RequestAugmentChoice();
                result.grantedAmount = reward.amount;
                break;
        }

        result.applied = true;
        return result;
    }
}
