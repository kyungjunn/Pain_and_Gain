using UnityEngine;

public abstract class ItemBase : MonoBehaviour, IInteractable
{
    [Header("ItemData")]
    [SerializeField] protected ItemData itemData;

    public ItemData ItemData => itemData;

    public virtual void Interact() 
    {
        // 기본 기능
    }

    public virtual string GetInteractText()
    {
        return "상호작용"; // 반드시 문자열을 반환해야함.
    }

    protected abstract void ApplyEffect(GameObject player);
}
