using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ItemBox : MonoBehaviour
{
    private enum BoxState
    {
        None,
        Prompt,
        Resolved
    }

    [Header("Available Items")]
    [SerializeField] private ItemData[] availableItems;

    [Header("Item Apply Prefabs")]
    [SerializeField] private GameObject equipApplyPrefab;
    [SerializeField] private GameObject consumableApplyPrefab;

    [Header("ItemBox UI")]
    [SerializeField] private GameObject uiPanel;
    [SerializeField] private Text infoText;

    [Header("Open Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private float effectDelay = 0.5f;
    [SerializeField] private float destroyDelay = 0.8f;

    [Header("Interact")]
    [SerializeField] private Key interactKey = Key.E;

    [Header("Messages")]
    [SerializeField] private string openPromptMessage = "상자 열기 (E)";
    [SerializeField] private string emptyBoxMessage = "빈 상자입니다.";

    [Header("Pickup Message")]
    [SerializeField] private float resultShowTime = 2f;

    private static readonly int OpenHash = Animator.StringToHash("Open");

    private ItemData chosenItem;
    private BoxState state = BoxState.None;
    private GameObject playerInRange;

    private void Awake()
    {
        // 상자 내 아이템 랜덤 설정
        chosenItem = PickRandomItem();

        if (chosenItem == null)
        {
            Debug.LogWarning($"[{name}] chosenItem이 null입니다.");
        }

        if (uiPanel != null) uiPanel.SetActive(false);
    }

    // 랜덤 아이템 뽑기
    private ItemData PickRandomItem()
    {
        if (availableItems == null || availableItems.Length == 0) return null;

        float[] weights = new float[availableItems.Length];
        float totalWeight = 0f;

        for (int i = 0; i < availableItems.Length; i++)
        {
            weights[i] = GetRarityWeight(availableItems[i].Rarity);
            totalWeight += weights[i];
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < availableItems.Length; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
                return availableItems[i];
        }

        return availableItems[availableItems.Length - 1];
    }

    private float GetRarityWeight(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Common: return 60f;
            case ItemRarity.Rare: return 30f;
            case ItemRarity.Epic: return 9f;
            case ItemRarity.Legendary: return 1f;
            default: return 10f;
        }
    }

    private void Update()
    {
        if (state != BoxState.Prompt || playerInRange == null) return;

        if (Keyboard.current != null && Keyboard.current[interactKey].wasPressedThisFrame)
        {
            OpenBox();
        }
    }

    private void ShowPrompt()
    {
        state = BoxState.Prompt;

        if (infoText != null) infoText.text = chosenItem == null ? emptyBoxMessage : openPromptMessage;
        if (uiPanel != null) uiPanel.SetActive(true);
    }

    private void OpenBox()
    {
        if (chosenItem == null)
        {
            Debug.LogWarning($"[{name}] chosenItem이 null입니다.");
            return;
        }

        state = BoxState.Resolved;

        GameObject player = playerInRange;

        if (uiPanel != null) uiPanel.SetActive(false);
        if (animator != null) animator.SetTrigger(OpenHash);

        StartCoroutine(ApplyAfterDelay(player));
    }

    private IEnumerator ApplyAfterDelay(GameObject player)
    {
        yield return new WaitForSeconds(effectDelay);

        ApplyItemEffect(player);

        if (infoText != null) infoText.text = BuildResultMessage(chosenItem);
        if (uiPanel != null) uiPanel.SetActive(true);

        yield return new WaitForSeconds(resultShowTime);

        if (uiPanel != null) uiPanel.SetActive(false);
        Destroy(gameObject, destroyDelay);
    }

    // 획득 메시지 생성
    private string BuildResultMessage(ItemData data)
    {
        string effect = data.ItemType == ItemType.Equipment
            ? $"Damage +{data.BonusAttackDamage}"
            : $"HP +{Mathf.RoundToInt(data.HpRecoveryAmount)} 회복";

        return $"{data.ItemName} 획득!\n{effect}";
    }

    // 아이템 효과 적용
    private void ApplyItemEffect(GameObject player)
    {
        if (chosenItem == null)
        {
            Debug.LogWarning($"[{name}] chosenItem이 null입니다.");
            return;
        }

        GameObject effectPrefab = chosenItem.ItemType == ItemType.Equipment ? equipApplyPrefab : consumableApplyPrefab;

        if (effectPrefab == null)
        {
            Debug.LogWarning($"[{name}] {chosenItem.ItemType} 타입에 연결된 Effect Prefab이 비어있습니다.");
            return;
        }

        GameObject tempItem = Instantiate(effectPrefab, transform.position, Quaternion.identity);
        ItemBase itemBase = tempItem.GetComponent<ItemBase>();

        if (itemBase != null)
        {
            itemBase.ApplyTo(player, chosenItem);
        }
        else
        {
            Debug.LogWarning($"[{name}] '{effectPrefab.name}' 프리팹에 ItemBase(EquipItem/ConsumableItem) 컴포넌트가 없습니다.");
        }

        Destroy(tempItem);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (state == BoxState.Resolved) return;

        if (other.CompareTag("Player"))
        {
            playerInRange = other.gameObject;
            ShowPrompt();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (state == BoxState.Resolved) return;

        if (other.CompareTag("Player"))
        {
            playerInRange = null;
            state = BoxState.None;

            if (uiPanel != null) uiPanel.SetActive(false);
        }
    }
}