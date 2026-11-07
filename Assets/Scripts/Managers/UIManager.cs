using UnityEngine;
using System.Collections.Generic;

// 선택권 큐·패널. 세션 종료 없음.
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Panels")]
    public AugmentPanelUI augmentPanelUI;
    public StatsPanelUI statsPanelUI;
    public LoadingPanelUI loadingPanelUI;

    private int pendingTickets;
    private readonly Queue<int> deferredRewardExperience = new Queue<int>();
    private readonly Queue<int> ticketRewardExperience = new Queue<int>();
    public int DeferredAugmentTickets => deferredRewardExperience.Count;
    private bool isAugmentPanelOpen;
    private int nextTicketId = 1;
    private int currentTicketId;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            // 중복 거부
            enabled = false;
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ShowLoadingUI(bool isShow)
    {
        if (loadingPanelUI == null)
        {
            return;
        }

        if (isShow)
        {
            loadingPanelUI.ResetLoadingUI();
            loadingPanelUI.gameObject.SetActive(true);
        }
        else
        {
            loadingPanelUI.gameObject.SetActive(false);
        }
    }

    public void RequestAugmentChoice()
    {
        // 비활성 세션: 티켓 거부
        if (GameManager.Instance == null || !GameManager.Instance.IsActiveSession) return;
        ticketRewardExperience.Enqueue(0);
        pendingTickets++;
        while (deferredRewardExperience.Count > 0)
        {
            ticketRewardExperience.Enqueue(deferredRewardExperience.Dequeue());
            pendingTickets++;
        }
        if (!isAugmentPanelOpen)
        {
            ShowNextTicket();
        }
    }

    public bool DeferAugmentChoice(int fallbackExperience = 0)
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsActiveSession ||
            augmentPanelUI == null || AugmentManager.Instance == null)
            return false;
        if (fallbackExperience <= 0)
        {
            var level = GameManager.Instance.CurrentPlayer?.GetComponent<PlayerLevelSystem>();
            fallbackExperience = level != null ? Mathf.Max(1, Mathf.RoundToInt(level.requiredExp * 0.2f)) : 1;
        }
        deferredRewardExperience.Enqueue(fallbackExperience);
        return true;
    }

    public void CompleteCurrentTicket()
    {
        if (pendingTickets > 0)
        {
            pendingTickets--;
            if (ticketRewardExperience.Count > 0) ticketRewardExperience.Dequeue();
        }

        if (pendingTickets > 0)
        {
            ShowNextTicket();
            return;
        }

        ClosePanelAndResume();
    }

    // 선택권·패널 무효
    public void ClearSession(bool resume)
    {
        pendingTickets = 0;
        deferredRewardExperience.Clear();
        ticketRewardExperience.Clear();
        currentTicketId = 0;
        isAugmentPanelOpen = false;

        if (augmentPanelUI != null)
        {
            augmentPanelUI.Invalidate();
            augmentPanelUI.gameObject.SetActive(false);
        }

        if (resume && GameManager.Instance != null)
        {
            GameManager.Instance.ResumeGame();
        }
    }

    private void ShowNextTicket()
    {
        if (augmentPanelUI == null || AugmentManager.Instance == null)
        {
            ClosePanelAndResume();
            return;
        }

        isAugmentPanelOpen = true;
        while (pendingTickets > 0 && ticketRewardExperience.Count > 0 &&
               ticketRewardExperience.Peek() > 0 && AugmentManager.Instance.AreAllAugmentsExhausted())
        {
            int experience = ticketRewardExperience.Dequeue();
            pendingTickets--;
            // 먼저 선택권을 소비해서 경험치 레벨업의 재진입에도 중복 지급하지 않는다.
            GameManager.Instance?.CurrentPlayer?.GetComponent<PlayerLevelSystem>()?.AddExp(experience);
        }
        if (pendingTickets == 0)
        {
            ClosePanelAndResume();
            return;
        }
        currentTicketId = nextTicketId++;
        int sessionId = GameManager.Instance?.SessionId ?? 0;
        var options = AugmentManager.Instance.GetRandomAugments();
        augmentPanelUI.gameObject.SetActive(true);

        if (options.Count == 0)
        {
            augmentPanelUI.ShowExhausted("선택할 증강이 없습니다. 이번 선택을 건너뛰려면 확인하세요.", sessionId, currentTicketId);
        }
        else
        {
            augmentPanelUI.Setup(options, sessionId, currentTicketId);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.PauseGame();
        }
    }

    private void ClosePanelAndResume()
    {
        isAugmentPanelOpen = false;
        currentTicketId = 0;

        if (augmentPanelUI != null)
        {
            augmentPanelUI.Invalidate();
            augmentPanelUI.gameObject.SetActive(false);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResumeGame();
        }
    }
}
