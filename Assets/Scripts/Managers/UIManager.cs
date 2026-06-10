using UnityEngine;
using System.Collections.Generic;

// 공용 UI 패널 표시와 증강 선택 패널 큐를 관리
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Panels")]
    public AugmentPanelUI augmentPanelUI;
    public StatsPanelUI statsPanelUI;
    public LoadingPanelUI loadingPanelUI;

    private readonly Queue<List<StatAugmentSO>> pendingAugmentPanels = new Queue<List<StatAugmentSO>>();
    private bool isAugmentPanelOpen;

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

    public void OpenAugmentPanel(List<StatAugmentSO> augments)
    {
        if (augmentPanelUI == null || augments == null)
        {
            return;
        }

        if (isAugmentPanelOpen)
        {
            // 여러 번 레벨업해도 증강 패널이 겹치지 않도록 대기열에 저장
            pendingAugmentPanels.Enqueue(augments);
            return;
        }

        ShowAugmentPanel(augments);
    }

    private void ShowAugmentPanel(List<StatAugmentSO> augments)
    {
        isAugmentPanelOpen = true;
        augmentPanelUI.gameObject.SetActive(true);
        augmentPanelUI.Setup(augments);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.PauseGame();
        }
    }

    public void CloseAugmentPanel()
    {
        if (pendingAugmentPanels.Count > 0)
        {
            // 현재 선택이 끝나면 다음 레벨업 증강을 순서대로 표시
            ShowAugmentPanel(pendingAugmentPanels.Dequeue());
            return;
        }

        isAugmentPanelOpen = false;
        augmentPanelUI.gameObject.SetActive(false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResumeGame();
        }
    }
}
