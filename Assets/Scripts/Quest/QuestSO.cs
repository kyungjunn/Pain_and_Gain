using UnityEngine;

// 퀘스트 목표
public enum QuestObjectiveType
{
    KillEnemies
}

// 실패 페널티
public enum QuestPenaltyType
{
    SkillRemove,
    StatReduce
}

[CreateAssetMenu(fileName = "New Quest", menuName = "Game/Quest")]
public class QuestSO : ScriptableObject
{
    // 표시 정보
    [Header("Info")]
    public string questName;

    [TextArea]
    public string description;

    // 성공 조건
    [Header("Objective")]
    public QuestObjectiveType objectiveType = QuestObjectiveType.KillEnemies;
    [Min(1)] public int targetAmount = 3;
    [Min(1f)] public float timeLimit = 30f;

    // 실패 설정
    [Header("Failure Penalty")]
    public QuestPenaltyType penaltyType = QuestPenaltyType.SkillRemove;
    [Range(0f, 1f)] public float statReduceMin = 0.25f;
    [Range(0f, 1f)] public float statReduceMax = 0.5f;

    private void OnValidate()
    {
        // 입력값 보정
        targetAmount = Mathf.Max(1, targetAmount);
        timeLimit = Mathf.Max(1f, timeLimit);
        statReduceMin = Mathf.Clamp01(statReduceMin);
        statReduceMax = Mathf.Clamp(statReduceMax, statReduceMin, 1f);
    }
}
