using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class AugmentInventoryUI : MonoBehaviour
{
    [SerializeField] private RectTransform content;
    [SerializeField] private AugmentInventorySlotUI template;
    private readonly List<AugmentInventorySlotUI> slots = new List<AugmentInventorySlotUI>();
    private PlayerAugments playerAugments;

    public void Configure(RectTransform content, AugmentInventorySlotUI template)
    {
        this.content = content;
        this.template = template;
    }

    private void OnEnable()
    {
        SpawnManager.OnPlayerSpawned += Bind;
        Bind(GameObject.FindGameObjectWithTag("Player"));
    }

    private void OnDisable()
    {
        SpawnManager.OnPlayerSpawned -= Bind;
        if (playerAugments != null) playerAugments.onSkillsChanged -= Refresh;
        playerAugments = null;
    }

    public void Bind(GameObject player)
    {
        // 이전 플레이어 구독 해제 · 현재 목록 재연결
        if (playerAugments != null) playerAugments.onSkillsChanged -= Refresh;
        playerAugments = player != null ? player.GetComponent<PlayerAugments>() : null;
        if (playerAugments != null) playerAugments.onSkillsChanged += Refresh;
        Refresh();
    }

    public void Refresh()
    {
        if (content == null || template == null) return;
        if (slots.Count == 0) slots.AddRange(content.GetComponentsInChildren<AugmentInventorySlotUI>(true));
        int count = playerAugments != null ? playerAugments.OwnedSkills.Count : 0;
        // 기본 12칸 유지 · 초과 슬롯 재사용
        int capacity = Mathf.Max(12, count);
        while (slots.Count < capacity)
            slots.Add(Instantiate(template, content));
        int index = 0;
        if (playerAugments != null)
            foreach (var augment in playerAugments.OwnedSkills)
            {
                slots[index].gameObject.SetActive(true);
                slots[index++].SetAugment(augment, playerAugments.GetSkillStackCount(augment));
            }
        for (; index < slots.Count; index++)
        {
            slots[index].SetAugment(null);
            slots[index].gameObject.SetActive(index < capacity);
        }
        ResizeGrid();
        LayoutRebuilder.MarkLayoutForRebuild(content);
    }

    private void OnRectTransformDimensionsChange() => ResizeGrid();

    private void ResizeGrid()
    {
        if (content == null || !content.TryGetComponent<GridLayoutGroup>(out var grid)) return;
        float width = (content.rect.width - grid.padding.horizontal - grid.spacing.x * 2) / 3;
        if (width > 0) grid.cellSize = new Vector2(width, width + 38);
    }
}
