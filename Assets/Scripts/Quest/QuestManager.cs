using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// 퀘스트 진행 상태
public enum QuestState
{
    WaitingForPlayer,
    Countdown,
    Preview,
    ChallengePreview,
    Active,
    Stopped
}

// 적용된 위험 도전 손실
public struct QuestPenaltyResult
{
    public string removedSkillName;
    public int remainingSkillStacks;

    public bool HasPenalty => !string.IsNullOrEmpty(removedSkillName);
}

// 길잡이·일반 퀘스트·위험 도전의 런타임 소유자
public class QuestManager : MonoBehaviour
{
    private const string TutorialCompleteKey = "QuestManager.TutorialComplete.v1";
    private readonly HashSet<QuestRewardSO> ownedRewards = new HashSet<QuestRewardSO>();

    private enum CountdownDestination
    {
        None,
        ResumeTutorial,
        StartNormalQuest,
        CreateChallenge
    }

    public static QuestManager Instance { get; private set; }

    [Header("Quest Definitions")]
    [SerializeField] private List<QuestSO> quests = new List<QuestSO>();
    [SerializeField] private List<QuestSO> tutorialQuests = new List<QuestSO>();
    [SerializeField] private QuestSO riskChallengeQuest;
    [SerializeField] private GameObject challengeZonePrefab;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float firstQuestDelay = 15f;
    [SerializeField, Min(0f)] private float questCooldown = 50f;
    [SerializeField, Min(0f)] private float normalPreviewDuration = 3f;
    [SerializeField, Min(0f)] private float challengeLifetime = 30f;
    [SerializeField, Min(0f)] private float challengeDwellDuration = 1.5f;

    [Header("Resolved Reward Amounts")]
    [SerializeField, Min(0.01f)] private float normalExperienceRatio = 0.2f;
    [SerializeField, Min(0.01f)] private float normalHealingRatio = 0.15f;
    [SerializeField, Min(0.01f)] private float tutorialHealingRatio = 0.1f;

    public QuestState State { get; private set; } = QuestState.WaitingForPlayer;
    public QuestSO CurrentQuest { get; private set; }
    public int CurrentProgress { get; private set; }
    public float RemainingTime { get; private set; }
    public QuestRewardSO CurrentReward { get; private set; }
    public float PreviewRemainingTime { get; private set; }
    public float PreviewDuration => normalPreviewDuration;
    public QuestChallengeZone ChallengeZone => challengeZone;
    public bool IsQuestTimerPaused => Time.timeScale <= 0f ||
        (GameManager.Instance != null && GameManager.Instance.IsPaused) || IsLoadingScreenVisible();

    public string CurrentRewardDescription
    {
        get
        {
            if (CurrentQuest == null)
            {
                return string.Empty;
            }

            if (CurrentQuest.category == QuestCategory.Tutorial)
            {
                if (CurrentQuest.objectiveType == QuestObjectiveType.KillEnemies)
                {
                    return "자연 처치 경험치를 반영해 첫 레벨업까지 부족한 경험치";
                }

                if (CurrentQuest.objectiveType == QuestObjectiveType.FirstAugment)
                {
                    return "선택한 증강 자체가 보상 (추가 보상 없음)";
                }
            }

            return CurrentReward != null ? CurrentReward.Description : "추가 보상 없음";
        }
    }

    public string CurrentRiskPenaltyDescription
    {
        get
        {
            if (string.IsNullOrEmpty(riskPenaltySkillName) || riskPenaltyStartingStacks < 2)
            {
                return "실패 손실 없음";
            }

            return $"{riskPenaltySkillName} 중첩 {riskPenaltyStartingStacks} → {riskPenaltyStartingStacks - 1}";
        }
    }

    public event Action<QuestSO> onQuestPreviewStarted;
    public event Action<float, float> onQuestPreviewTimeChanged;
    public event Action<QuestSO> onQuestStarted;
    public event Action<int, int> onQuestProgressChanged;
    public event Action<float, float> onQuestTimeChanged;
    public event Action<QuestSO, QuestRewardResult> onQuestSucceeded;
    public event Action<QuestSO, QuestPenaltyResult> onQuestFailed;
    public event Action onChallengeZoneChanged;

    private PlayerStats playerStats;
    private PlayerAugments playerAugments;
    private PlayerSkillController playerSkillController;
    private PlayerDamageDealer damageDealer;
    private PlayerLevelSystem playerLevelSystem;
    private GameObject player;
    private PlayerHealth playerHealth;
    private int boundSessionId;

    private float countdown;
    private float previewCountdown;
    private CountdownDestination countdownDestination;
    private int currentTutorialIndex;
    private int runKillCount;
    private int runSkillHitCount;
    private int runQSkillHitCount;
    private int tutorialStartingLevel = 1;
    private bool hasAcquiredAugmentThisRun;

    private EnemyHealth activeEventMonster;
    private GameObject activeEventBeacon;
    private QuestChallengeZone challengeZone;
    private AugmentSkill riskPenaltySkill;
    private int riskPenaltyStartingStacks;
    private string riskPenaltySkillName;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            enabled = false;
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        if (Instance != this)
        {
            return;
        }

        SpawnManager.OnPlayerSpawned += HandlePlayerSpawned;
        EnemyHealth.OnEnemyKilled += HandleEnemyKilled;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;

        SpawnManager spawnManager = SpawnManager.Instance;
        if (spawnManager != null)
        {
            HandlePlayerSpawned(spawnManager.SpawnedPlayer);
        }
    }

    private void OnDisable()
    {
        SpawnManager.OnPlayerSpawned -= HandlePlayerSpawned;
        EnemyHealth.OnEnemyKilled -= HandleEnemyKilled;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        UnbindPlayerActions();

        if (Instance == this)
        {
            ClearSession();
        }
    }

    private void Update()
    {
        if (Instance != this || HaltIfSessionInactive())
        {
            return;
        }

        if (player == null)
        {
            return;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            ClearSession();
            return;
        }

        if (IsQuestTimerPaused)
        {
            return;
        }

        if (State == QuestState.WaitingForPlayer)
        {
            TryStartRunWhenReady();
            if (State == QuestState.WaitingForPlayer)
            {
                return;
            }
        }

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        switch (State)
        {
            case QuestState.Countdown:
                countdown = Mathf.Max(0f, countdown - deltaTime);
                if (countdown <= 0f)
                {
                    RunCountdownDestination();
                }
                break;

            case QuestState.Preview:
                previewCountdown = Mathf.Max(0f, previewCountdown - deltaTime);
                PreviewRemainingTime = previewCountdown;
                onQuestPreviewTimeChanged?.Invoke(previewCountdown, normalPreviewDuration);
                if (previewCountdown <= 0f)
                {
                    StartPreviewedQuest();
                }
                break;

            case QuestState.Active:
                if (CurrentQuest == null || CurrentQuest.category == QuestCategory.Tutorial)
                {
                    break;
                }

                RemainingTime = Mathf.Max(0f, RemainingTime - deltaTime);
                onQuestTimeChanged?.Invoke(RemainingTime, CurrentQuest.timeLimit);
                break;
        }
    }

    private void LateUpdate()
    {
        if (Instance == this && !IsQuestTimerPaused && State == QuestState.Active && CurrentQuest != null &&
            CurrentQuest.category != QuestCategory.Tutorial && RemainingTime <= 0f && CanResolveQuest())
        {
            FailCurrentQuest();
        }
    }

    private void HandlePlayerSpawned(GameObject playerObject)
    {
        int sessionId = GameManager.Instance != null ? GameManager.Instance.SessionId : 0;
        if (playerObject != null && playerObject == player && sessionId == boundSessionId)
        {
            BindPlayerActions();
            return;
        }

        UnbindPlayerActions();
        ClearChallengeZone();
        ClearCurrentQuest();
        ClearRiskPenaltyTarget();
        player = playerObject;
        boundSessionId = sessionId;
        playerHealth = playerObject != null ? playerObject.GetComponent<PlayerHealth>() : null;
        playerStats = playerObject != null ? playerObject.GetComponent<PlayerStats>() : null;
        playerAugments = playerObject != null ? playerObject.GetComponent<PlayerAugments>() : null;
        playerSkillController = playerObject != null ? playerObject.GetComponent<PlayerSkillController>() : null;
        damageDealer = playerObject != null ? playerObject.GetComponent<PlayerDamageDealer>() : null;
        playerLevelSystem = playerObject != null ? playerObject.GetComponent<PlayerLevelSystem>() : null;

        ResetRunActionMemory();
        State = QuestState.WaitingForPlayer;
        if (playerObject != null)
        {
            BindPlayerActions();
            TryStartRunWhenReady();
        }
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        if (player != null || State != QuestState.Stopped)
        {
            ClearSession();
        }
    }

    public void ClearSession()
    {
        State = QuestState.Stopped;
        countdown = 0f;
        previewCountdown = 0f;
        PreviewRemainingTime = 0f;
        countdownDestination = CountdownDestination.None;
        ClearChallengeZone();
        ClearCurrentQuest();
        ClearRiskPenaltyTarget();
        UnbindPlayerActions();
        player = null;
        playerHealth = null;
        playerStats = null;
        playerAugments = null;
        playerSkillController = null;
        damageDealer = null;
        playerLevelSystem = null;
        boundSessionId = 0;
        ResetRunActionMemory();
    }

    private void TryStartRunWhenReady()
    {
        if (State != QuestState.WaitingForPlayer || IsQuestTimerPaused || !CanRecordAction())
        {
            return;
        }

        if (IsTutorialComplete())
        {
            BeginCountdown(firstQuestDelay, CountdownDestination.StartNormalQuest);
            return;
        }

        currentTutorialIndex = 0;
        StartTutorialQuest();
    }

    private bool HaltIfSessionInactive()
    {
        if (IsCombatSessionActive())
        {
            return false;
        }

        if (State != QuestState.Stopped || player != null)
        {
            ClearSession();
        }

        return true;
    }

    private void BindPlayerActions()
    {
        UnbindPlayerActions();
        if (playerStats != null)
        {
            playerStats.onStatsChanged += HandleAugmentChanged;
        }

        if (playerAugments != null)
        {
            playerAugments.onSkillsChanged += HandleAugmentChanged;
        }

        if (damageDealer != null)
        {
            damageDealer.OnDamageDealt += HandleDamageDealt;
            damageDealer.OnSkillTargetDamaged += HandleSkillTargetDamaged;
        }

        hasAcquiredAugmentThisRun |= HasAnyAugment();
    }

    private void UnbindPlayerActions()
    {
        if (playerStats != null)
        {
            playerStats.onStatsChanged -= HandleAugmentChanged;
        }

        if (playerAugments != null)
        {
            playerAugments.onSkillsChanged -= HandleAugmentChanged;
        }

        if (damageDealer != null)
        {
            damageDealer.OnDamageDealt -= HandleDamageDealt;
            damageDealer.OnSkillTargetDamaged -= HandleSkillTargetDamaged;
        }
    }

    private void HandleAugmentChanged()
    {
        if (HasAnyAugment())
        {
            hasAcquiredAugmentThisRun = true;
        }

        if (State == QuestState.Active && CurrentQuest != null &&
            CurrentQuest.objectiveType == QuestObjectiveType.FirstAugment && hasAcquiredAugmentThisRun)
        {
            AdvanceProgress(1);
        }
    }

    private void HandleDamageDealt(int actualDamage, PlayerDamageType damageType)
    {
        if (!CanRecordAction() || actualDamage <= 0 || damageType != PlayerDamageType.Skill)
        {
            return;
        }

        runSkillHitCount++;
        if (State == QuestState.Active && CurrentQuest != null &&
            CurrentQuest.objectiveType == QuestObjectiveType.SkillHits)
        {
            AdvanceProgress(1);
        }
    }

    private void HandleSkillTargetDamaged(EnemyHealth enemy, int actualDamage, PlayerSkillSO skill)
    {
        if (!CanRecordAction() || enemy == null || actualDamage <= 0 || playerSkillController == null ||
            skill == null || skill != playerSkillController.SkillQ)
        {
            return;
        }

        runQSkillHitCount++;
        if (State == QuestState.Active && CurrentQuest != null &&
            CurrentQuest.objectiveType == QuestObjectiveType.QSkillHits)
        {
            AdvanceProgress(1);
        }
    }

    private void HandleEnemyKilled(EnemyHealth enemy)
    {
        if (!CanRecordAction() || enemy == null)
        {
            return;
        }

        runKillCount++;
        if (!CanResolveQuest())
        {
            return;
        }

        if (CurrentQuest.objectiveType == QuestObjectiveType.HuntEventMonster)
        {
            if (enemy == activeEventMonster)
            {
                AdvanceProgress(1);
            }

            return;
        }

        if (CurrentQuest.objectiveType == QuestObjectiveType.KillEnemies)
        {
            AdvanceProgress(1);
        }
    }

    private bool CanRecordAction()
    {
        GameManager gameManager = GameManager.Instance;
        return player != null && player.activeInHierarchy && boundSessionId != 0 && gameManager != null &&
            gameManager.BelongsToSession(boundSessionId) && gameManager.CurrentPlayer == player &&
            (playerHealth == null || !playerHealth.IsDead);
    }

    private bool CanResolveQuest()
    {
        return CanRecordAction() && State == QuestState.Active && CurrentQuest != null;
    }

    private void AdvanceProgress(int amount)
    {
        if (!CanResolveQuest() || amount <= 0)
        {
            return;
        }

        CurrentProgress = Mathf.Min(CurrentProgress + amount, CurrentQuest.targetAmount);
        onQuestProgressChanged?.Invoke(CurrentProgress, CurrentQuest.targetAmount);
        if (CurrentProgress >= CurrentQuest.targetAmount)
        {
            SucceedCurrentQuest();
        }
    }

    private void StartTutorialQuest()
    {
        if (!CanRecordAction())
        {
            return;
        }

        if (tutorialQuests == null || tutorialQuests.Count != 4 || currentTutorialIndex < 0 ||
            currentTutorialIndex >= tutorialQuests.Count)
        {
            Debug.LogError("QuestManager requires four ordered tutorial QuestSO definitions.", this);
            State = QuestState.Stopped;
            return;
        }

        QuestSO quest = tutorialQuests[currentTutorialIndex];
        if (quest == null || quest.category != QuestCategory.Tutorial ||
            quest.objectiveType != GetExpectedTutorialObjective(currentTutorialIndex) ||
            !IsTutorialDefinitionValid(quest))
        {
            Debug.LogError($"Invalid tutorial quest at index {currentTutorialIndex}.", this);
            State = QuestState.Stopped;
            return;
        }

        if (!TryResolveQuestReward(quest, out QuestRewardSO reward))
        {
            Debug.LogError($"Tutorial reward is invalid: {quest.questName}", this);
            State = QuestState.Stopped;
            return;
        }

        CurrentQuest = quest;
        CurrentReward = reward;
        CurrentProgress = GetRememberedProgress(quest);
        RemainingTime = 0f;
        PreviewRemainingTime = 0f;

        if (quest.objectiveType == QuestObjectiveType.HuntEventMonster && !TrySpawnEventMonster(quest))
        {
            Debug.LogWarning($"Tutorial target could not spawn; retrying the same step: {quest.questName}", this);
            ClearCurrentQuest();
            BeginCountdown(5f, CountdownDestination.ResumeTutorial);
            return;
        }

        State = QuestState.Active;
        onQuestStarted?.Invoke(CurrentQuest);
        onQuestProgressChanged?.Invoke(CurrentProgress, CurrentQuest.targetAmount);
        onQuestTimeChanged?.Invoke(0f, 0f);

        if (CurrentProgress >= CurrentQuest.targetAmount)
        {
            SucceedCurrentQuest();
        }
    }

    private static QuestObjectiveType GetExpectedTutorialObjective(int index)
    {
        switch (index)
        {
            case 0:
                return QuestObjectiveType.KillEnemies;
            case 1:
                return QuestObjectiveType.QSkillHits;
            case 2:
                return QuestObjectiveType.FirstAugment;
            case 3:
                return QuestObjectiveType.HuntEventMonster;
            default:
                throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    private bool IsTutorialDefinitionValid(QuestSO quest)
    {
        switch (quest.objectiveType)
        {
            case QuestObjectiveType.KillEnemies:
                return quest.successReward is ExperienceRewardSO;
            case QuestObjectiveType.QSkillHits:
                return quest.successReward is HealRewardSO && HasUsableQDamageSkill();
            case QuestObjectiveType.FirstAugment:
                return quest.successReward == null;
            case QuestObjectiveType.HuntEventMonster:
                return quest.successReward is ExperienceRewardSO && quest.eventMonsterPrefab != null &&
                    quest.eventMonsterBeaconPrefab != null;
            default:
                return false;
        }
    }

    private int GetRememberedProgress(QuestSO quest)
    {
        int progress = 0;
        switch (quest.objectiveType)
        {
            case QuestObjectiveType.KillEnemies:
                progress = runKillCount;
                break;
            case QuestObjectiveType.SkillHits:
                progress = runSkillHitCount;
                break;
            case QuestObjectiveType.QSkillHits:
                progress = runQSkillHitCount;
                break;
            case QuestObjectiveType.FirstAugment:
                progress = hasAcquiredAugmentThisRun ? 1 : 0;
                break;
        }

        return Mathf.Min(progress, quest.targetAmount);
    }

    private void StartNormalQuestPreview()
    {
        if (!CanRecordAction())
        {
            return;
        }

        List<QuestSO> candidates = new List<QuestSO>();
        if (quests != null)
        {
            foreach (QuestSO quest in quests)
            {
                if (IsNormalDefinitionValid(quest) && HasEligibleReward(quest))
                {
                    candidates.Add(quest);
                }
            }
        }

        if (candidates.Count == 0)
        {
            Debug.LogError("QuestManager requires at least one valid normal quest and reward pool.", this);
            State = QuestState.Stopped;
            return;
        }

        QuestSO selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        if (!TryResolveQuestReward(selected, out QuestRewardSO reward))
        {
            BeginCountdown(5f, CountdownDestination.StartNormalQuest);
            return;
        }

        CurrentQuest = selected;
        CurrentReward = reward;
        CurrentProgress = 0;
        RemainingTime = 0f;
        previewCountdown = normalPreviewDuration;
        PreviewRemainingTime = previewCountdown;
        State = QuestState.Preview;

        onQuestPreviewStarted?.Invoke(CurrentQuest);
        onQuestPreviewTimeChanged?.Invoke(previewCountdown, normalPreviewDuration);
    }

    private bool IsNormalDefinitionValid(QuestSO quest)
    {
        if (quest == null || quest.category != QuestCategory.Normal || quest.rewardPool == null ||
            quest.timeLimit <= 0f)
        {
            return false;
        }

        switch (quest.objectiveType)
        {
            case QuestObjectiveType.KillEnemies:
                return true;
            case QuestObjectiveType.SkillHits:
                return HasUsableDamageSkill();
            case QuestObjectiveType.HuntEventMonster:
                return quest.eventMonsterPrefab != null && quest.eventMonsterBeaconPrefab != null;
            default:
                return false;
        }
    }

    private bool HasUsableDamageSkill()
    {
        return playerSkillController != null && playerSkillController.isActiveAndEnabled &&
            damageDealer != null && damageDealer.isActiveAndEnabled &&
            (playerSkillController.SkillQ != null || playerSkillController.SkillE != null);
    }

    private bool HasUsableQDamageSkill()
    {
        return playerSkillController != null && playerSkillController.isActiveAndEnabled &&
            damageDealer != null && damageDealer.isActiveAndEnabled && playerSkillController.SkillQ != null;
    }

    private bool HasEligibleReward(QuestSO quest)
    {
        return quest.rewardPool != null && quest.rewardPool.TrySelect(
            0f,
            reward => IsRewardTypeValid(quest, reward) && reward.CanApply(player),
            out _);
    }

    private bool IsRewardTypeValid(QuestSO quest, QuestRewardSO reward)
    {
        if (reward == null)
        {
            return false;
        }

        if (quest.category == QuestCategory.RiskChallenge)
        {
            return reward is AugmentChoiceRewardSO;
        }

        if (quest.category == QuestCategory.Tutorial)
        {
            return false;
        }

        switch (quest.objectiveType)
        {
            case QuestObjectiveType.KillEnemies:
                return reward is ExperienceRewardSO;
            case QuestObjectiveType.SkillHits:
                return reward is HealRewardSO;
            case QuestObjectiveType.HuntEventMonster:
                return reward is ExperienceRewardSO || reward is HealRewardSO;
            default:
                return false;
        }
    }

    private bool TryResolveQuestReward(QuestSO quest, out QuestRewardSO reward)
    {
        reward = null;
        if (quest == null)
        {
            return false;
        }

        QuestRewardSO source;
        if (quest.category == QuestCategory.Tutorial)
        {
            if (quest.objectiveType == QuestObjectiveType.FirstAugment)
            {
                return true;
            }

            source = quest.successReward;
        }
        else
        {
            if (quest.rewardPool == null || !quest.rewardPool.TrySelect(
                    UnityEngine.Random.value,
                    candidate => IsRewardTypeValid(quest, candidate) && candidate.CanApply(player),
                    out source))
            {
                return false;
            }
        }

        if (source == null || !source.CanApply(player))
        {
            return false;
        }

        reward = Instantiate(source);
        if (reward == null)
        {
            return false;
        }

        ownedRewards.Add(reward);
        if (reward is ExperienceRewardSO experience)
        {
            if (quest.category == QuestCategory.Normal)
            {
                experience.amount = CalculateRewardAmount(playerLevelSystem, normalExperienceRatio);
            }
        }
        else if (reward is HealRewardSO healing)
        {
            float ratio = quest.category == QuestCategory.Tutorial ? tutorialHealingRatio : normalHealingRatio;
            healing.amount = Mathf.Max(1, Mathf.RoundToInt((playerHealth != null ? playerHealth.MaxHealth : 1) * ratio));
        }
        else if (reward is AugmentChoiceRewardSO choice)
        {
            choice.fallbackExperience = CalculateRewardAmount(playerLevelSystem, normalExperienceRatio);
        }

        if (!reward.CanApply(player))
        {
            DestroyReward(reward);
            reward = null;
            return false;
        }

        return true;
    }

    private void DestroyReward(QuestRewardSO reward)
    {
        if (reward == null || !ownedRewards.Remove(reward))
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(reward);
        }
        else
        {
            DestroyImmediate(reward);
        }
    }

    private int CalculateRewardAmount(PlayerLevelSystem levelSystem, float ratio)
    {
        int requiredExperience = levelSystem != null ? Mathf.Max(1, levelSystem.requiredExp) : 1;
        return Mathf.Max(1, Mathf.RoundToInt(requiredExperience * ratio));
    }

    private void StartPreviewedQuest()
    {
        if (State != QuestState.Preview || CurrentQuest == null || !CanRecordAction())
        {
            return;
        }

        if (CurrentQuest.objectiveType == QuestObjectiveType.HuntEventMonster && !TrySpawnEventMonster(CurrentQuest))
        {
            Debug.LogWarning($"Normal quest canceled because its target could not spawn: {CurrentQuest.questName}", this);
            ClearCurrentQuest();
            BeginCountdown(questCooldown, CountdownDestination.CreateChallenge);
            return;
        }

        StartActiveQuest();
    }

    private void StartActiveQuest()
    {
        if (CurrentQuest == null || !CanRecordAction())
        {
            return;
        }

        State = QuestState.Active;
        RemainingTime = CurrentQuest.category == QuestCategory.Tutorial ? 0f : CurrentQuest.timeLimit;
        onQuestStarted?.Invoke(CurrentQuest);
        onQuestProgressChanged?.Invoke(CurrentProgress, CurrentQuest.targetAmount);
        onQuestTimeChanged?.Invoke(RemainingTime, CurrentQuest.timeLimit);

        if (CurrentProgress >= CurrentQuest.targetAmount)
        {
            SucceedCurrentQuest();
        }
    }

    private bool TrySpawnEventMonster(QuestSO quest)
    {
        ClearEventMonster();
        GameObject monster = null;
        if (SpawnManager.Instance == null || quest == null || quest.eventMonsterPrefab == null ||
            !SpawnManager.Instance.TrySpawnQuestEnemy(quest.eventMonsterPrefab, out monster) || monster == null ||
            !monster.TryGetComponent(out activeEventMonster))
        {
            if (monster != null)
            {
                Destroy(monster);
            }

            activeEventMonster = null;
            return false;
        }

        if (quest.eventMonsterBeaconPrefab != null)
        {
            activeEventBeacon = Instantiate(quest.eventMonsterBeaconPrefab, monster.transform);
            activeEventBeacon.transform.localPosition = Vector3.zero;
            activeEventBeacon.transform.localRotation = Quaternion.identity;
        }

        return true;
    }

    private void CreateChallengeZone()
    {
        if (!CanRecordAction())
        {
            return;
        }

        if (!IsRiskDefinitionValid(riskChallengeQuest) || !TrySelectRiskPenaltyTarget(out AugmentSkill penaltyTarget) ||
            !TryFindChallengePosition(out Vector3 position) ||
            !TryResolveQuestReward(riskChallengeQuest, out QuestRewardSO reward))
        {
            Debug.Log("[Quest] 위험 도전을 이번 순환에서 건너뜁니다: 유효한 목표·손실 대상·보상·위치가 없습니다.", this);
            BeginCountdown(questCooldown, CountdownDestination.StartNormalQuest);
            return;
        }

        if (challengeZonePrefab == null)
        {
            Debug.LogError("QuestManager requires a challenge zone prefab for risk challenges.", this);
            DestroyReward(reward);
            BeginCountdown(questCooldown, CountdownDestination.StartNormalQuest);
            return;
        }

        GameObject instance = Instantiate(challengeZonePrefab, position, Quaternion.identity);
        QuestChallengeZone zone = instance.GetComponent<QuestChallengeZone>();
        if (zone == null || !zone.Configure(this, player.transform, challengeLifetime, challengeDwellDuration))
        {
            Debug.LogError("Challenge zone prefab must contain a configured QuestChallengeZone.", instance);
            Destroy(instance);
            DestroyReward(reward);
            BeginCountdown(questCooldown, CountdownDestination.StartNormalQuest);
            return;
        }

        CurrentQuest = riskChallengeQuest;
        CurrentReward = reward;
        CurrentProgress = 0;
        RemainingTime = 0f;
        riskPenaltySkill = penaltyTarget;
        riskPenaltyStartingStacks = penaltyTarget.StackCount;
        riskPenaltySkillName = GetRiskPenaltyName(penaltyTarget);
        challengeZone = zone;
        zone.onStartRequested += HandleChallengeStartRequested;
        zone.onExpired += HandleChallengeExpired;
        zone.onVisibilityChanged += HandleChallengeVisibilityChanged;
        State = QuestState.ChallengePreview;
        onChallengeZoneChanged?.Invoke();
    }

    private bool IsRiskDefinitionValid(QuestSO quest)
    {
        return quest != null && quest.category == QuestCategory.RiskChallenge &&
            quest.objectiveType == QuestObjectiveType.HuntEventMonster && quest.targetAmount == 1 &&
            quest.timeLimit > 0f && quest.eventMonsterPrefab != null && quest.eventMonsterBeaconPrefab != null &&
            quest.rewardPool != null;
    }

    private bool TrySelectRiskPenaltyTarget(out AugmentSkill target)
    {
        target = null;
        if (playerAugments == null)
        {
            return false;
        }

        List<AugmentSkill> candidates = new List<AugmentSkill>();
        foreach (AugmentSkill skill in playerAugments.OwnedSkillInstances)
        {
            if (skill != null && !skill.IsReleased && skill.StackCount >= 2)
            {
                candidates.Add(skill);
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        target = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        return true;
    }

    private bool TryFindChallengePosition(out Vector3 position)
    {
        position = default;
        if (player == null)
        {
            return false;
        }

        NavMeshPath path = new NavMeshPath();
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector2 direction = UnityEngine.Random.insideUnitCircle;
            if (direction.sqrMagnitude < 0.01f)
            {
                continue;
            }

            direction.Normalize();
            float distance = UnityEngine.Random.Range(12f, 25f);
            Vector3 candidate = player.transform.position + new Vector3(direction.x * distance, 0f, direction.y * distance);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                continue;
            }

            if (NavMesh.CalculatePath(player.transform.position, hit.position, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete)
            {
                position = hit.position;
                return true;
            }
        }

        return false;
    }

    private void HandleChallengeStartRequested(QuestChallengeZone requestedZone)
    {
        if (requestedZone != challengeZone || State != QuestState.ChallengePreview ||
            CurrentQuest != riskChallengeQuest || !CanRecordAction())
        {
            return;
        }

        if (!IsRiskPenaltyAvailable() || CurrentReward == null || !CurrentReward.CanApply(player) ||
            !TrySpawnEventMonster(CurrentQuest))
        {
            Debug.LogWarning("Risk challenge start canceled because its fixed target, reward, or monster spawn is no longer valid.", this);
            CancelChallengeWithoutFailure();
            return;
        }

        ClearChallengeZone();
        StartActiveQuest();
        onChallengeZoneChanged?.Invoke();
    }

    private void HandleChallengeExpired(QuestChallengeZone expiredZone)
    {
        if (expiredZone != challengeZone || State != QuestState.ChallengePreview)
        {
            return;
        }

        ClearChallengeWithoutFailure();
    }

    private void HandleChallengeVisibilityChanged(QuestChallengeZone changedZone)
    {
        if (changedZone == challengeZone)
        {
            onChallengeZoneChanged?.Invoke();
        }
    }

    private void CancelChallengeWithoutFailure(string reason = null)
    {
        if (reason != null)
        {
            Debug.Log($"[Quest] {reason}", this);
        }

        ClearChallengeWithoutFailure();
    }

    private void ClearChallengeWithoutFailure()
    {
        ClearChallengeZone();
        ClearCurrentQuest();
        ClearRiskPenaltyTarget();
        BeginCountdown(questCooldown, CountdownDestination.StartNormalQuest);
    }

    private void SucceedCurrentQuest()
    {
        if (!CanResolveQuest())
        {
            return;
        }

        QuestSO completedQuest = CurrentQuest;
        QuestRewardSO reward = CurrentReward;
        bool completedTutorial = completedQuest.category == QuestCategory.Tutorial;
        if (completedTutorial)
        {
            currentTutorialIndex++;
            if (currentTutorialIndex >= tutorialQuests.Count)
            {
                PlayerPrefs.SetInt(TutorialCompleteKey, 1);
                PlayerPrefs.Save();
                BeginCountdown(firstQuestDelay, CountdownDestination.StartNormalQuest);
            }
            else
            {
                State = QuestState.Stopped;
            }
        }
        else if (completedQuest.category == QuestCategory.RiskChallenge)
        {
            BeginCountdown(questCooldown, CountdownDestination.StartNormalQuest);
        }
        else
        {
            BeginCountdown(questCooldown, CountdownDestination.CreateChallenge);
        }

        ClearChallengeZone();
        ClearCurrentQuest(false);
        QuestRewardResult result = ApplySuccessReward(completedQuest, reward);
        ClearRiskPenaltyTarget();
        onQuestSucceeded?.Invoke(completedQuest, result);
        DestroyReward(reward);

        if (completedTutorial && State == QuestState.Stopped)
        {
            StartTutorialQuest();
        }
    }

    private QuestRewardResult ApplySuccessReward(QuestSO quest, QuestRewardSO reward)
    {
        if (quest.category == QuestCategory.Tutorial && quest.objectiveType == QuestObjectiveType.FirstAugment)
        {
            return new QuestRewardResult { reward = null, applied = true, grantedAmount = 0f };
        }

        if (quest.category == QuestCategory.Tutorial && quest.objectiveType == QuestObjectiveType.KillEnemies &&
            reward is ExperienceRewardSO experience)
        {
            int requiredAmount = 0;
            if (playerLevelSystem != null && playerLevelSystem.level <= tutorialStartingLevel)
            {
                requiredAmount = Mathf.Max(0, playerLevelSystem.requiredExp - playerLevelSystem.currentExp);
            }

            if (requiredAmount <= 0)
            {
                return new QuestRewardResult { reward = reward, applied = true, grantedAmount = 0f };
            }

            experience.amount = requiredAmount;
        }

        if (reward == null)
        {
            return default;
        }

        return reward.Apply(player);
    }

    private void FailCurrentQuest()
    {
        if (!CanResolveQuest())
        {
            return;
        }

        QuestSO failedQuest = CurrentQuest;
        QuestRewardSO unearnedReward = CurrentReward;
        QuestPenaltyResult penaltyResult = default;

        BeginCountdown(
            questCooldown,
            failedQuest.category == QuestCategory.RiskChallenge
                ? CountdownDestination.StartNormalQuest
                : CountdownDestination.CreateChallenge);
        ClearChallengeZone();
        ClearCurrentQuest();

        if (failedQuest.category == QuestCategory.RiskChallenge)
        {
            penaltyResult = ApplyFixedRiskPenalty();
        }

        ClearRiskPenaltyTarget();
        DestroyReward(unearnedReward);
        onQuestFailed?.Invoke(failedQuest, penaltyResult);
    }

    private QuestPenaltyResult ApplyFixedRiskPenalty()
    {
        QuestPenaltyResult result = default;
        AugmentSkill target = riskPenaltySkill;
        if (target == null || target.IsReleased || target.StackCount <= 1 || playerAugments == null ||
            !target.TryRemoveStack())
        {
            return result;
        }

        result.removedSkillName = riskPenaltySkillName;
        result.remainingSkillStacks = target.StackCount;
        playerAugments.onSkillsChanged?.Invoke();
        return result;
    }

    private bool IsRiskPenaltyAvailable()
    {
        return riskPenaltySkill != null && !riskPenaltySkill.IsReleased && riskPenaltyStartingStacks >= 2 &&
            riskPenaltySkill.StackCount == riskPenaltyStartingStacks;
    }

    private string GetRiskPenaltyName(AugmentSkill skill)
    {
        if (skill == null)
        {
            return "증강";
        }

        SkillAugmentSO definition = playerAugments != null ? playerAugments.GetSkillDefinition(skill) : null;
        if (definition != null && !string.IsNullOrEmpty(definition.augmentName))
        {
            return definition.augmentName;
        }

        string name = skill.gameObject.name;
        const string cloneSuffix = "(Clone)";
        if (name.EndsWith(cloneSuffix, StringComparison.Ordinal))
        {
            name = name.Substring(0, name.Length - cloneSuffix.Length).TrimEnd();
        }

        return name;
    }

    private void BeginCountdown(float duration, CountdownDestination destination)
    {
        countdown = Mathf.Max(0f, duration);
        countdownDestination = destination;
        State = QuestState.Countdown;
    }

    private void RunCountdownDestination()
    {
        CountdownDestination destination = countdownDestination;
        countdownDestination = CountdownDestination.None;
        switch (destination)
        {
            case CountdownDestination.ResumeTutorial:
                StartTutorialQuest();
                break;
            case CountdownDestination.StartNormalQuest:
                StartNormalQuestPreview();
                break;
            case CountdownDestination.CreateChallenge:
                CreateChallengeZone();
                break;
            default:
                State = QuestState.Stopped;
                break;
        }
    }

    private void ClearCurrentQuest(bool destroyReward = true)
    {
        ClearEventMonster();
        if (destroyReward)
        {
            DestroyReward(CurrentReward);
        }

        CurrentReward = null;
        CurrentQuest = null;
        CurrentProgress = 0;
        RemainingTime = 0f;
        PreviewRemainingTime = 0f;
        previewCountdown = 0f;
    }

    private void ClearEventMonster()
    {
        ClearEventBeacon();
        if (activeEventMonster != null)
        {
            Destroy(activeEventMonster.gameObject);
        }

        activeEventMonster = null;
    }

    private void ClearEventBeacon()
    {
        if (activeEventBeacon != null)
        {
            Destroy(activeEventBeacon);
        }

        activeEventBeacon = null;
    }

    private void ClearChallengeZone()
    {
        if (challengeZone == null)
        {
            return;
        }

        challengeZone.onStartRequested -= HandleChallengeStartRequested;
        challengeZone.onExpired -= HandleChallengeExpired;
        challengeZone.onVisibilityChanged -= HandleChallengeVisibilityChanged;
        Destroy(challengeZone.gameObject);
        challengeZone = null;
        onChallengeZoneChanged?.Invoke();
    }

    private void ClearRiskPenaltyTarget()
    {
        riskPenaltySkill = null;
        riskPenaltyStartingStacks = 0;
        riskPenaltySkillName = null;
    }

    private void ResetRunActionMemory()
    {
        currentTutorialIndex = 0;
        runKillCount = 0;
        runSkillHitCount = 0;
        runQSkillHitCount = 0;
        hasAcquiredAugmentThisRun = HasAnyAugment();
        tutorialStartingLevel = playerLevelSystem != null ? playerLevelSystem.level : 1;
    }

    private bool HasAnyAugment()
    {
        bool hasStatAugment = playerStats != null && playerStats.GetAugmentedStatTypes().Count > 0;
        bool hasSkillAugment = playerAugments != null && playerAugments.OwnedSkills.Count > 0;
        return hasStatAugment || hasSkillAugment;
    }

    private static bool IsTutorialComplete()
    {
        return PlayerPrefs.GetInt(TutorialCompleteKey, 0) == 1;
    }

    private static bool IsCombatSessionActive()
    {
        return GameManager.Instance != null && GameManager.Instance.IsActiveSession;
    }

    private static bool IsLoadingScreenVisible()
    {
        return UIManager.Instance != null && UIManager.Instance.loadingPanelUI != null &&
            UIManager.Instance.loadingPanelUI.gameObject.activeInHierarchy;
    }

    private void OnValidate()
    {
        firstQuestDelay = Mathf.Max(0f, firstQuestDelay);
        questCooldown = Mathf.Max(0f, questCooldown);
        normalPreviewDuration = Mathf.Max(0f, normalPreviewDuration);
        challengeLifetime = Mathf.Max(0f, challengeLifetime);
        challengeDwellDuration = Mathf.Max(0f, challengeDwellDuration);
        normalExperienceRatio = Mathf.Max(0.01f, normalExperienceRatio);
        normalHealingRatio = Mathf.Max(0.01f, normalHealingRatio);
        tutorialHealingRatio = Mathf.Max(0.01f, tutorialHealingRatio);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            ClearSession();
            Instance = null;
        }
    }
}
