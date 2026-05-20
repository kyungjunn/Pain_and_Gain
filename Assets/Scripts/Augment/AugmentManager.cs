using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class AugmentManager : MonoBehaviour
{
    public PlayerStats playerStats;

    [Header("All Augments")]
    public List<StatAugmentSO> allAugments;

    [Header("Settings")]
    public int optionCount = 3;

    [Header("UI")]
    public GameObject augmentPanel;
    public Transform optionParent;
    public AugmentOptionUI optionPrefab;

    public void ShowRandomAugments()
    {
        augmentPanel.SetActive(true);

        // 기존 UI 제거
        foreach (Transform child in optionParent)
        {
            Destroy(child.gameObject);
        }

        List<StatAugmentSO> selected =
            allAugments
            .OrderBy(x => Random.value)
            .Take(optionCount)
            .ToList();

        foreach (var augment in selected)
        {
            AugmentOptionUI ui =
                Instantiate(optionPrefab, optionParent);

            ui.Setup(augment, this);
        }

        Time.timeScale = 0f;
    }

    public void ApplyAugment(StatAugmentSO augment)
    {
        playerStats.ApplyStat(
            augment.type,
            augment.value
        );

        augmentPanel.SetActive(false);

        Time.timeScale = 1f;
    }
}