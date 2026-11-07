using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// QuestManager 이벤트를 받아 현재 퀘스트 HUD와 성공/실패 토스트를 표시한다.
public class QuestPanelUI : MonoBehaviour
{
    [Header("Quest Card")]
    public CanvasGroup questCardGroup;
    public TextMeshProUGUI questNameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI progressText;
    public TextMeshProUGUI timerText;
    public CanvasGroup penaltyGroup;
    public TextMeshProUGUI penaltyText;
    public Image timerFillImage;

    [Header("Preview and Risk Notice")]
    public TextMeshProUGUI previewTimerText;
    public CanvasGroup riskNoticeGroup;
    public TextMeshProUGUI riskNoticeText;
    public TextMeshProUGUI dwellProgressText;

    [Header("Deferred Augment Choices")]
    public TextMeshProUGUI deferredTicketsText;

    [Header("Result Toast")]
    public CanvasGroup resultToastGroup;
    public Image resultBackgroundImage;
    public TextMeshProUGUI resultTitleText;
    public TextMeshProUGUI resultMessageText;

    [Header("Colors")]
    public Color successColor = new Color(0.08f, 0.28f, 0.16f, 0.92f);
    public Color failureColor = new Color(0.33f, 0.07f, 0.11f, 0.94f);

    [SerializeField] private float resultDuration = 2.5f;

    private QuestManager questManager;
    private Coroutine hideResultRoutine;
    private int displayedDeferredTickets = -1;

    private void Awake()
    {
        HideQuestCard();
        HideResultToast();
    }

    private void OnEnable()
    {
        TryBindQuestManager();
    }

    private void OnDisable()
    {
        HideQuestCard();
        UnbindQuestManager();

        if (hideResultRoutine != null)
        {
            StopCoroutine(hideResultRoutine);
            hideResultRoutine = null;
        }
    }

    private void Update()
    {
        if (questManager == null)
        {
            TryBindQuestManager();
        }
        else if (questManager.State == QuestState.ChallengePreview)
        {
            RefreshChallengeNotice();
        }
        else if (questManager.State == QuestState.Stopped || questManager.State == QuestState.WaitingForPlayer)
        {
            HideQuestCard();
        }

        RefreshDeferredTickets();
    }

    private void TryBindQuestManager()
    {
        if (questManager != null || QuestManager.Instance == null)
        {
            return;
        }

        questManager = QuestManager.Instance;
        questManager.onQuestPreviewStarted += HandleQuestPreviewStarted;
        questManager.onQuestPreviewTimeChanged += UpdatePreviewTime;
        questManager.onQuestStarted += HandleQuestStarted;
        questManager.onQuestProgressChanged += HandleQuestProgressChanged;
        questManager.onQuestTimeChanged += UpdateTime;
        questManager.onQuestSucceeded += HandleQuestSucceeded;
        questManager.onQuestFailed += HandleQuestFailed;
        questManager.onChallengeZoneChanged += HandleChallengeZoneChanged;

        RefreshFromManager();
    }

    private void UnbindQuestManager()
    {
        if (questManager == null)
        {
            return;
        }

        questManager.onQuestStarted -= HandleQuestStarted;
        questManager.onQuestProgressChanged -= HandleQuestProgressChanged;
        questManager.onQuestTimeChanged -= UpdateTime;
        questManager.onQuestSucceeded -= HandleQuestSucceeded;
        questManager.onQuestFailed -= HandleQuestFailed;
        questManager.onChallengeZoneChanged -= HandleChallengeZoneChanged;
        questManager.onQuestPreviewStarted -= HandleQuestPreviewStarted;
        questManager.onQuestPreviewTimeChanged -= UpdatePreviewTime;
        questManager = null;
    }

    private void RefreshFromManager()
    {
        if (questManager == null || questManager.CurrentQuest == null)
        {
            HideQuestCard();
            return;
        }

        switch (questManager.State)
        {
            case QuestState.Active:
                HandleQuestStarted(questManager.CurrentQuest);
                HandleQuestProgressChanged(questManager.CurrentProgress, questManager.CurrentQuest.targetAmount);
                UpdateTime(questManager.RemainingTime, questManager.CurrentQuest.timeLimit);
                break;
            case QuestState.Preview:
                HandleQuestPreviewStarted(questManager.CurrentQuest);
                HandleQuestProgressChanged(questManager.CurrentProgress, questManager.CurrentQuest.targetAmount);
                UpdatePreviewTime(questManager.PreviewRemainingTime, questManager.PreviewDuration);
                break;
            case QuestState.ChallengePreview:
                RefreshChallengeNotice();
                break;
            default:
                HideQuestCard();
                break;
        }
    }

    private void HandleQuestPreviewStarted(QuestSO quest)
    {
        if (quest == null)
        {
            HideQuestCard();
            return;
        }

        HideRiskNotice();
        SetCanvasGroupVisible(questCardGroup, true);
        SetQuestDetails(quest);
        HandleQuestProgressChanged(0, quest.targetAmount);
        UpdatePreviewTime(questManager.PreviewRemainingTime, questManager.PreviewDuration);
    }

    private void HandleQuestStarted(QuestSO quest)
    {
        if (quest == null)
        {
            HideQuestCard();
            return;
        }

        HideRiskNotice();
        SetCanvasGroupVisible(questCardGroup, true);
        SetQuestDetails(quest);
        if (previewTimerText != null)
        {
            previewTimerText.text = string.Empty;
        }

        HandleQuestProgressChanged(0, quest.targetAmount);
        UpdateTime(quest.timeLimit, quest.timeLimit);
    }

    private void SetQuestDetails(QuestSO quest)
    {
        if (questNameText != null)
        {
            questNameText.text = quest.questName;
        }

        if (descriptionText != null)
        {
            descriptionText.text = $"{quest.description}\n성공 시: {questManager.CurrentRewardDescription}";
        }

        if (penaltyText != null)
        {
            penaltyText.text = quest.category == QuestCategory.RiskChallenge
                ? questManager.CurrentRiskPenaltyDescription
                : "실패 시 능력 손실 없음";
        }

        SetCanvasGroupVisible(penaltyGroup, true);
    }

    private void HandleQuestProgressChanged(int current, int target)
    {
        if (progressText != null)
        {
            progressText.text = string.Format("{0} / {1}", current, target);
        }
    }

    private void UpdateTime(float remaining, float total)
    {
        if (timerText != null)
        {
            timerText.text = total > 0f ? FormatTime(remaining) : string.Empty;
        }

        if (timerFillImage != null)
        {
            timerFillImage.fillAmount = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
        }
    }

    private void UpdatePreviewTime(float remaining, float total)
    {
        if (previewTimerText != null)
        {
            previewTimerText.text = $"시작까지 {FormatTime(remaining)}";
        }

        if (timerText != null)
        {
            timerText.text = $"시작까지 {FormatTime(remaining)}";
        }

        if (timerFillImage != null)
        {
            timerFillImage.fillAmount = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
        }
    }

    private void HandleChallengeZoneChanged()
    {
        if (questManager != null && questManager.State == QuestState.ChallengePreview)
        {
            RefreshChallengeNotice();
        }
        else
        {
            HideRiskNotice();
        }
    }

    private void RefreshChallengeNotice()
    {
        QuestChallengeZone zone = questManager != null ? questManager.ChallengeZone : null;
        QuestSO quest = questManager != null ? questManager.CurrentQuest : null;
        bool visible = zone != null && quest != null && zone.IsPlayerInWarningArea &&
            !questManager.IsQuestTimerPaused && CanShowRiskNotice();

        SetCanvasGroupVisible(questCardGroup, false);
        SetCanvasGroupVisible(penaltyGroup, false);
        SetCanvasGroupVisible(riskNoticeGroup, visible);

        if (!visible)
        {
            if (zone != null)
            {
                zone.SetNoticeVisible(false);
            }

            return;
        }

        if (riskNoticeText != null)
        {
            riskNoticeText.text =
                $"! {quest.questName}\n{quest.description}\n" +
                $"시작 후 {FormatTime(quest.timeLimit)} 안에 표시된 몬스터 처치\n" +
                $"성공: {questManager.CurrentRewardDescription}\n" +
                $"실패: {questManager.CurrentRiskPenaltyDescription}\n" +
                $"안쪽 구역에 {zone.DwellDuration:0.0}초 머무르면 시작 · 지나치면 손실 없음";
        }

        if (dwellProgressText != null)
        {
            dwellProgressText.text =
                $"시작 진행: {zone.DwellElapsed:0.0} / {zone.DwellDuration:0.0}초";
        }

        zone.SetNoticeVisible(true);
    }

    private bool CanShowRiskNotice()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy || riskNoticeGroup == null ||
            riskNoticeText == null || dwellProgressText == null || !riskNoticeGroup.gameObject.activeInHierarchy ||
            !riskNoticeText.isActiveAndEnabled || !dwellProgressText.isActiveAndEnabled)
        {
            return false;
        }

        for (Transform ancestor = riskNoticeGroup.transform.parent; ancestor != null; ancestor = ancestor.parent)
        {
            CanvasGroup group = ancestor.GetComponent<CanvasGroup>();
            if (group != null && group.alpha <= 0f)
            {
                return false;
            }
        }

        return true;
    }

    private void HandleQuestSucceeded(QuestSO quest, QuestRewardResult result)
    {
        HideQuestCard();
        RefreshDeferredTickets();
        ShowResultToast(true, "퀘스트 성공", result.Description);
    }

    private void HandleQuestFailed(QuestSO quest, QuestPenaltyResult result)
    {
        HideQuestCard();
        bool riskChallenge = quest != null && quest.category == QuestCategory.RiskChallenge;
        string title = riskChallenge ? "위험 도전 실패" : "퀘스트 시간 초과";
        string message = riskChallenge
            ? FormatPenaltyResult(result)
            : "보상 없음 · 능력 손실 없음";
        ShowResultToast(false, title, message);
        if (!riskChallenge && resultBackgroundImage != null)
            resultBackgroundImage.color = new Color(0.12f, 0.16f, 0.2f, 0.92f);
    }

    private void ShowResultToast(bool success, string title, string message)
    {
        if (resultBackgroundImage != null)
        {
            resultBackgroundImage.color = success ? successColor : failureColor;
        }

        if (resultTitleText != null)
        {
            resultTitleText.text = title;
        }

        if (resultMessageText != null)
        {
            resultMessageText.text = message;
        }

        SetCanvasGroupVisible(resultToastGroup, true);

        if (hideResultRoutine != null)
        {
            StopCoroutine(hideResultRoutine);
        }

        hideResultRoutine = StartCoroutine(HideResultAfterDelay());
    }

    private IEnumerator HideResultAfterDelay()
    {
        yield return new WaitForSecondsRealtime(resultDuration);
        HideResultToast();
        hideResultRoutine = null;
    }

    private void HideQuestCard()
    {
        SetCanvasGroupVisible(questCardGroup, false);
        SetCanvasGroupVisible(penaltyGroup, false);
        SetCanvasGroupVisible(riskNoticeGroup, false);

        if (questManager != null && questManager.ChallengeZone != null)
        {
            questManager.ChallengeZone.SetNoticeVisible(false);
        }
    }

    private void HideRiskNotice()
    {
        SetCanvasGroupVisible(riskNoticeGroup, false);
        if (questManager != null && questManager.ChallengeZone != null)
        {
            questManager.ChallengeZone.SetNoticeVisible(false);
        }
    }

    private void RefreshDeferredTickets()
    {
        if (deferredTicketsText == null)
        {
            return;
        }

        int count = UIManager.Instance != null ? UIManager.Instance.DeferredAugmentTickets : 0;
        if (displayedDeferredTickets == count)
        {
            return;
        }

        displayedDeferredTickets = count;
        deferredTicketsText.text = $"보관된 증강 선택권: {count}";
        deferredTicketsText.enabled = count > 0;
    }

    private void HideResultToast()
    {
        SetCanvasGroupVisible(resultToastGroup, false);
    }

    private static void SetCanvasGroupVisible(CanvasGroup group, bool visible)
    {
        if (group == null)
        {
            return;
        }

        group.alpha = visible ? 1f : 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private static string FormatPenaltyResult(QuestPenaltyResult result)
    {
        if (result.HasPenalty)
        {
            return $"{result.removedSkillName} 중첩 -1 (남은 중첩 {result.remainingSkillStacks})";
        }

        return "손실 대상이 없어 능력 손실 없음";
    }

    private static string FormatTime(float seconds)
    {
        int clampedSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
        int minutes = clampedSeconds / 60;
        int remainingSeconds = clampedSeconds % 60;
        return string.Format("{0:00}:{1:00}", minutes, remainingSeconds);
    }

    private void OnValidate()
    {
        resultDuration = Mathf.Max(0f, resultDuration);
    }
}
