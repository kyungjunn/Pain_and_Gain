using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.UI;

public class ItemBox : MonoBehaviour
{
    [Header("Available Items")]
    [SerializeField] private ItemData[] availableItems;

    [Header("ItemBox UI")]
    [SerializeField] private GameObject uiPanel;
    [SerializeField] private Text infoText;

    private ItemData chosenItem;
    private bool isOpened = false;

    private void Awake()
    {
        // 상자 내 아이템 랜덤 설정
        if (availableItems != null && availableItems.Length > 0)
        {
            int randomIndex = Random.Range(0, availableItems.Length);
            chosenItem = availableItems[randomIndex];
        }

        if (uiPanel != null) uiPanel.SetActive(false);
    }

    // 아이템 상호작용 텍스트
    public string GetInteractText()
    {
        if (chosenItem == null) return "빈 상자";

        if (chosenItem.ItemType == ItemType.Equipment)
            return $"[장비] {chosenItem.ItemName} 발견";
        else
            return $"[소모품] {chosenItem.ItemName} 발견";
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isOpened) return;

        if (other.CompareTag("Player"))
        {
            Debug.Log($"[아이템] {other.name} 상호작용");

            infoText.text = GetInteractText();
            uiPanel.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (uiPanel != null)
        {
            uiPanel.SetActive(false);
        }
    }
}