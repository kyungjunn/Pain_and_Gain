using UnityEngine;

public abstract class ItemBase : MonoBehaviour, IInteractable
{
    [Header("ItemData")]
    [SerializeField] protected ItemData itemData;

    public ItemData ItemData => itemData;

    [ContextMenu("강제 상호작용 테스트 버튼")]
    public virtual void Interact() 
    {
        Debug.Log($"[아이템] {itemData.ItemName} 과(와) 상호작용 했습니다");

        ApplyEffect(null);
    }

    public virtual string GetInteractText()
    {
        return $"{itemData.ItemName} 획득";
    }

    protected abstract void ApplyEffect(GameObject player);
}
