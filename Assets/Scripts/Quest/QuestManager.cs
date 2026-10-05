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
    public int remainingSkillStacks;
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
    public QuestReward CurrentReward { get; private set; }

    // UI 연동 이벤트
    public event Action<QuestSO> onQuestStarted;
    public event Action<int, int> onQuestProgressChanged;
    public event Action<float, float> onQuestTimeChanged;
    public event Action<QuestSO, QuestRewardResult> onQuestSucceeded;
    public event Action<QuestSO, QuestPenaltyResult> onQuestFailed;

    private PlayerStats playerStats;
    private PlayerAugments playerAugments;
    private GameObject player;
    private PlayerHealth playerHealth;
    private float countdown;
    private EnemyHealth activeEventMonster;
    private GameObject activeEventBeacon;

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
        if (player == null || (playerHealth != null && playerHealth.IsDead))
        {
            if (State != QuestState.Stopped)
            {
                State = QuestState.Stopped;
                ClearEventMonster();
                ClearCurrentQuest();
            }
            return;
        }

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
        ClearEventMonster();
        // 플레이어 바인딩
        player = playerObject;
        playerHealth = playerObject != null ? playerObject.GetComponent<PlayerHealth>() : null;
        playerStats = playerObject != null ? playerObject.GetComponent<PlayerStats>() : null;
        playerAugments = playerObject != null ? playerObject.GetComponent<PlayerAugments>() : null;
        CurrentQuest = null;
        CurrentProgress = 0;
        RemainingTime = 0f;
        State = QuestState.WaitingForAugment;
        CurrentReward = default;
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
        if (CurrentQuest.rewardPool == null
            || !CurrentQuest.rewardPool.TrySelect(
                UnityEngine.Random.value,
                selected => QuestRewardProcessor.CanApply(player, selected),
                out QuestReward reward))
        {
            Debug.LogWarning($"[Quest] 보상 설정 또는 지급 대상이 유효하지 않습니다: {CurrentQuest.questName}");
            ClearCurrentQuest();
            BeginCountdown(questCooldown);
            return;
        }
        CurrentReward = reward;
        if (CurrentQuest.objectiveType == QuestObjectiveType.HuntEventMonster)
        {
            GameObject monster = null;
            if (SpawnManager.Instance == null || CurrentQuest.eventMonsterPrefab == null
                || !SpawnManager.Instance.TrySpawnQuestEnemy(CurrentQuest.eventMonsterPrefab, out monster)
                || !monster.TryGetComponent(out activeEventMonster))
            {
                if (monster != null) Destroy(monster);
                Debug.LogWarning("이벤트 몬스터를 스폰할 수 없어 퀘스트를 다시 대기합니다.");
                ClearCurrentQuest();
                BeginCountdown(5f);
                return;
            }

            if (CurrentQuest.eventMonsterBeaconPrefab != null)
            {
                activeEventBeacon = Instantiate(CurrentQuest.eventMonsterBeaconPrefab, monster.transform);
                activeEventBeacon.transform.localPosition = Vector3.zero;
                activeEventBeacon.transform.localRotation = Quaternion.identity;
            }
        }
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
        if (!CanResolveQuest())
        {
            return;
        }

        if (CurrentQuest.objectiveType == QuestObjectiveType.HuntEventMonster && enemy != activeEventMonster)
        {
            return;
        }

        if (CurrentQuest.objectiveType != QuestObjectiveType.KillEnemies
            && CurrentQuest.objectiveType != QuestObjectiveType.HuntEventMonster) return;

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
        if (!CanResolveQuest()) return;
        QuestSO completedQuest = CurrentQuest;
        QuestReward reward = CurrentReward;
        // 지급 중 재진입해도 같은 퀘스트를 다시 완료할 수 없도록 먼저 종료한다.
        BeginCountdown(questCooldown);
        ClearCurrentQuest();
        QuestRewardResult result = QuestRewardProcessor.Apply(player, reward);
        if (!result.applied) Debug.LogError($"[Quest] 보상 지급 실패: {completedQuest.questName}");
        Debug.Log($"[Quest] 성공: {completedQuest.questName}, {result.Description}");
        onQuestSucceeded?.Invoke(completedQuest, result);
    }

    private void FailCurrentQuest()
    {
        if (!CanResolveQuest()) return;
        QuestSO failedQuest = CurrentQuest;
        BeginCountdown(questCooldown);
        ClearEventMonster();
        ClearCurrentQuest();
        QuestPenaltyResult penaltyResult = ApplyPenalty(failedQuest);
        LogFailure(failedQuest, penaltyResult);
        onQuestFailed?.Invoke(failedQuest, penaltyResult);
    }

    private bool CanResolveQuest()
    {
        return State == QuestState.Active && CurrentQuest != null && player != null
            && (playerHealth == null || !playerHealth.IsDead);
    }

    private QuestPenaltyResult ApplyPenalty(QuestSO quest)
    {
        // 설정된 능력만 박탈하며 다른 종류의 능력에 손실을 전가하지 않는다.
        QuestPenaltyResult result = new QuestPenaltyResult();

        if (quest == null || AugmentManager.Instance == null)
        {
            return result;
        }

        if (quest.penaltyType == QuestPenaltyType.SkillRemove)
        {
            result.removedSkill = AugmentManager.Instance.RemoveRandomSkill();
            result.remainingSkillStacks = playerAugments != null
                ? playerAugments.GetSkillStackCount(result.removedSkill)
                : 0;
            return result;
        }

        if (quest.penaltyType == QuestPenaltyType.StatReduce)
        {
            result.reducedStat = AugmentManager.Instance.ReduceRandomStat(
                quest.statReduceMin,
                quest.statReduceMax);
        }

        return result;
    }

    private void LogFailure(QuestSO quest, QuestPenaltyResult result)
    {
        if (result.removedSkill != null)
        {
            Debug.Log($"[Quest] 실패: {quest.questName}, 증강 중첩 감소: {result.removedSkill.augmentName}, 남은 중첩: {result.remainingSkillStacks}");
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
        ClearEventBeacon();
        activeEventMonster = null;
        CurrentQuest = null;
        CurrentProgress = 0;
        RemainingTime = 0f;
        CurrentReward = default;
    }

    private void ClearEventMonster()
    {
        ClearEventBeacon();
        if (activeEventMonster != null) Destroy(activeEventMonster.gameObject);
        activeEventMonster = null;
    }

    private void ClearEventBeacon()
    {
        if (activeEventBeacon != null) Destroy(activeEventBeacon);
        activeEventBeacon = null;
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
