using System;
using System.Collections.Generic;
using UnityEngine;

// 퀘스트 진행 상태
public enum QuestState
{
    WaitingForAugment,
    Countdown,
    Active,
    Stopped
}

// 페널티 실행 결과
public struct QuestPenaltyResult
{
    public SkillAugmentSO removedSkill;
    public StatReduceResult? reducedStat;

    public bool HasPenalty => removedSkill != null || reducedStat.HasValue;
}

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    // 퀘스트 후보
    [Header("Quests")]
    [SerializeField] private List<QuestSO> quests = new List<QuestSO>();

    // 발생 간격
    [Header("Timing")]
    [SerializeField] private float firstQuestDelay = 12f;
    [SerializeField] private float questCooldown = 45f;

    public QuestState State { get; private set; } = QuestState.WaitingForAugment;
    public QuestSO CurrentQuest { get; private set; }
    public int CurrentProgress { get; private set; }
    public float RemainingTime { get; private set; }

    // UI 연동 이벤트
    public event Action<QuestSO> onQuestStarted;
    public event Action<int, int> onQuestProgressChanged;
    public event Action<float, float> onQuestTimeChanged;
    public event Action<QuestSO> onQuestSucceeded;
    public event Action<QuestSO, QuestPenaltyResult> onQuestFailed;

    private PlayerStats playerStats;
    private PlayerAugments playerAugments;
    private float countdown;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        SpawnManager.OnPlayerSpawned += HandlePlayerSpawned;
        EnemyHealth.OnEnemyKilled += HandleEnemyKilled;
    }

    private void OnDisable()
    {
        SpawnManager.OnPlayerSpawned -= HandlePlayerSpawned;
        EnemyHealth.OnEnemyKilled -= HandleEnemyKilled;
    }

    private void Update()
    {
        // 상태별 시간 처리
        switch (State)
        {
            case QuestState.WaitingForAugment:
                if (HasAnyAugment())
                {
                    BeginCountdown(firstQuestDelay);
                }
                break;

            case QuestState.Countdown:
                countdown -= Time.deltaTime;

                if (countdown <= 0f)
                {
                    StartRandomQuest();
                }
                break;

            case QuestState.Active:
                RemainingTime = Mathf.Max(0f, RemainingTime - Time.deltaTime);
                onQuestTimeChanged?.Invoke(RemainingTime, CurrentQuest.timeLimit);

                if (RemainingTime <= 0f)
                {
                    FailCurrentQuest();
                }
                break;
        }
    }

    private void HandlePlayerSpawned(GameObject playerObject)
    {
        // 플레이어 바인딩
        playerStats = playerObject != null ? playerObject.GetComponent<PlayerStats>() : null;
        playerAugments = playerObject != null ? playerObject.GetComponent<PlayerAugments>() : null;
        CurrentQuest = null;
        CurrentProgress = 0;
        RemainingTime = 0f;
        State = QuestState.WaitingForAugment;
    }

    private bool HasAnyAugment()
    {
        // 첫 퀘스트 조건
        bool hasStatAugment = playerStats != null && playerStats.GetAugmentedStatTypes().Count > 0;
        bool hasSkillAugment = playerAugments != null && playerAugments.OwnedSkills.Count > 0;
        return hasStatAugment || hasSkillAugment;
    }

    private void BeginCountdown(float duration)
    {
        countdown = Mathf.Max(0f, duration);
        State = QuestState.Countdown;
    }

    private void StartRandomQuest()
    {
        // 랜덤 퀘스트 시작
        RemoveInvalidQuests();

        if (quests.Count == 0)
        {
            Debug.LogWarning("QuestManager에 유효한 QuestSO가 없습니다.");
            State = QuestState.Stopped;
            return;
        }

        CurrentQuest = quests[UnityEngine.Random.Range(0, quests.Count)];
        CurrentProgress = 0;
        RemainingTime = CurrentQuest.timeLimit;
        State = QuestState.Active;

        Debug.Log($"[Quest] 시작: {CurrentQuest.questName} ({CurrentQuest.targetAmount}, {CurrentQuest.timeLimit:0.#}초)");
        onQuestStarted?.Invoke(CurrentQuest);
        onQuestProgressChanged?.Invoke(CurrentProgress, CurrentQuest.targetAmount);
        onQuestTimeChanged?.Invoke(RemainingTime, CurrentQuest.timeLimit);
    }

    private void HandleEnemyKilled(EnemyHealth enemy)
    {
        // 처치 진행도
        if (State != QuestState.Active || CurrentQuest == null || CurrentQuest.objectiveType != QuestObjectiveType.KillEnemies)
        {
            return;
        }

        CurrentProgress = Mathf.Min(CurrentProgress + 1, CurrentQuest.targetAmount);
        Debug.Log($"[Quest] 진행: {CurrentQuest.questName} {CurrentProgress}/{CurrentQuest.targetAmount}");
        onQuestProgressChanged?.Invoke(CurrentProgress, CurrentQuest.targetAmount);

        if (CurrentProgress >= CurrentQuest.targetAmount)
        {
            SucceedCurrentQuest();
        }
    }

    private void SucceedCurrentQuest()
    {
        QuestSO completedQuest = CurrentQuest;
        ClearCurrentQuest();
        BeginCountdown(questCooldown);
        Debug.Log($"[Quest] 성공: {completedQuest.questName}");
        onQuestSucceeded?.Invoke(completedQuest);
    }

    private void FailCurrentQuest()
    {
        QuestSO failedQuest = CurrentQuest;
        QuestPenaltyResult penaltyResult = ApplyPenalty(failedQuest);
        ClearCurrentQuest();
        BeginCountdown(questCooldown);
        LogFailure(failedQuest, penaltyResult);
        onQuestFailed?.Invoke(failedQuest, penaltyResult);
    }

    private QuestPenaltyResult ApplyPenalty(QuestSO quest)
    {
        // 스킬 박탈 실패 시 스탯 감소
        QuestPenaltyResult result = new QuestPenaltyResult();

        if (quest == null || AugmentManager.Instance == null)
        {
            return result;
        }

        if (quest.penaltyType == QuestPenaltyType.SkillRemove)
        {
            result.removedSkill = AugmentManager.Instance.RemoveRandomSkill();

            if (result.removedSkill != null)
            {
                return result;
            }
        }

        result.reducedStat = AugmentManager.Instance.ReduceRandomStat(
            quest.statReduceMin,
            quest.statReduceMax);

        return result;
    }

    private void LogFailure(QuestSO quest, QuestPenaltyResult result)
    {
        if (result.removedSkill != null)
        {
            Debug.Log($"[Quest] 실패: {quest.questName}, 스킬 박탈: {result.removedSkill.augmentName}");
            return;
        }

        if (result.reducedStat.HasValue)
        {
            StatReduceResult stat = result.reducedStat.Value;
            Debug.Log($"[Quest] 실패: {quest.questName}, 스탯 감소: {stat.type} -{stat.amount:0.##}");
            return;
        }

        Debug.Log($"[Quest] 실패: {quest.questName}, 페널티 없음");
    }

    private void ClearCurrentQuest()
    {
        CurrentQuest = null;
        CurrentProgress = 0;
        RemainingTime = 0f;
    }

    private void RemoveInvalidQuests()
    {
        quests.RemoveAll(quest => quest == null);
    }

    private void OnValidate()
    {
        firstQuestDelay = Mathf.Max(0f, firstQuestDelay);
        questCooldown = Mathf.Max(0f, questCooldown);
    }
}
