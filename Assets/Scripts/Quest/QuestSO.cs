using UnityEngine;

public enum QuestCategory
{
    Tutorial,
    Normal,
    RiskChallenge
}

// 퀘스트 목표
public enum QuestObjectiveType
{
    KillEnemies,
    HuntEventMonster,
    SkillHits,
    QSkillHits,
    FirstAugment
}

public enum SkillHitCountingRule
{
    EveryDamageEvent
}

[CreateAssetMenu(fileName = "New Quest", menuName = "Game/Quest")]
public class QuestSO : ScriptableObject
{
    // 표시 정보
    [Header("Info")]
    public string questName;

    [TextArea]
    public string description;

    [Header("Quest Type")]
    public QuestCategory category = QuestCategory.Normal;

    // 성공 조건
    [Header("Objective")]
    public QuestObjectiveType objectiveType = QuestObjectiveType.KillEnemies;
    [Tooltip("EveryDamageEvent counts each PlayerDamageDealer event that reports positive actual skill damage, including each damaging multi-hit tick.")]
    public SkillHitCountingRule skillHitCountingRule = SkillHitCountingRule.EveryDamageEvent;
    [Min(1)] public int targetAmount = 3;
    [Tooltip("길잡이는 시간 제한 없음. 일반·위험 도전은 최소 1초.")]
    [Min(0f)] public float timeLimit = 30f;
    public GameObject eventMonsterPrefab;
    public GameObject eventMonsterBeaconPrefab;

    [Header("Success Reward")]
    [Tooltip("Tutorial rewards are fixed references; the manager clones them before applying runtime amounts.")]
    public QuestRewardSO successReward;
    public QuestRewardPoolSO rewardPool;

    private void OnValidate()
    {
        targetAmount = Mathf.Max(1, targetAmount);
        if (objectiveType == QuestObjectiveType.HuntEventMonster || objectiveType == QuestObjectiveType.FirstAugment)
        {
            targetAmount = 1;
        }

        timeLimit = category == QuestCategory.Tutorial
            ? Mathf.Max(0f, timeLimit)
            : Mathf.Max(1f, timeLimit);
    }
}
