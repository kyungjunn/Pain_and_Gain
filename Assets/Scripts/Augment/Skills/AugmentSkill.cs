using UnityEngine;

// 스킬 증강 하나의 실제 동작.
// OnApply에서 효과를 켜고, Release에서 반드시 원복한다.
public abstract class AugmentSkill : MonoBehaviour
{
    protected GameObject Player { get; private set; }
    public int StackCount { get; private set; }
    public bool IsReleased { get; private set; }

    public void Apply(GameObject player)
    {
        Player = player;
        StackCount = 1;
        IsReleased = false;
        OnApply();
    }

    public void AddStack()
    {
        StackCount++;
        OnStackChanged();
    }

    public bool TryRemoveStack()
    {
        if (StackCount <= 1)
        {
            return false;
        }

        StackCount--;
        OnStackChanged();
        return true;
    }

    public void Release()
    {
        if (IsReleased)
        {
            return;
        }

        IsReleased = true;
        OnRemove();
    }

    private void OnDestroy()
    {
        Release();
    }

    protected abstract void OnApply();
    protected abstract void OnRemove();

    protected virtual void OnStackChanged()
    {
    }
}
