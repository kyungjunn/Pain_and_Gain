using UnityEngine;

public abstract class ItemBase : MonoBehaviour, IInteractable
{
    [Header("ItemData")]
    [SerializeField] protected ItemData itemData;

    public ItemData ItemData => itemData;

    public virtual void Interact() 
    {
        ApplyEffect(null);
    }

    public virtual string GetInteractText()
    {
        return $"{itemData.ItemName} 획득";
    }

    protected abstract void ApplyEffect(GameObject player);
}
