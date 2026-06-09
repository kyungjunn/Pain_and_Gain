using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 레벨업 시 표시되는 증강 선택 패널
public class AugmentPanelUI : MonoBehaviour
{
    [SerializeField] private float selectionDelay = 0.35f;

    public List<AugmentOptionUI> optionUIs;

    private Coroutine enableSelectionRoutine;

    public void Setup(List<StatAugmentSO> augments)
    {
        if (enableSelectionRoutine != null)
        {
            StopCoroutine(enableSelectionRoutine);
            enableSelectionRoutine = null;
        }

        for (int i = 0; i < optionUIs.Count; i++)
        {
            // 이전 레벨업 선택지가 남지 않도록 모든 옵션을 먼저 비활성화
            optionUIs[i].SetInteractable(false);
            optionUIs[i].gameObject.SetActive(false);
        }

        for (int i = 0; i < augments.Count && i < optionUIs.Count; i++)
        {
            optionUIs[i].gameObject.SetActive(true);
            optionUIs[i].Setup(augments[i]);
            optionUIs[i].SetInteractable(false);
        }

        enableSelectionRoutine = StartCoroutine(EnableSelectionAfterDelay());
    }

    // 전투 중 광클로 증강을 바로 눌러버리는 상황을 막기 위한 짧은 유예
    private IEnumerator EnableSelectionAfterDelay()
    {
        yield return new WaitForSecondsRealtime(selectionDelay);

        for (int i = 0; i < optionUIs.Count; i++)
        {
            if (optionUIs[i].gameObject.activeSelf)
            {
                optionUIs[i].SetInteractable(true);
            }
        }

        enableSelectionRoutine = null;
    }

    private void OnDisable()
    {
        if (enableSelectionRoutine != null)
        {
            StopCoroutine(enableSelectionRoutine);
            enableSelectionRoutine = null;
        }
    }

    private void OnValidate()
    {
        selectionDelay = Mathf.Max(0f, selectionDelay);
    }
}
