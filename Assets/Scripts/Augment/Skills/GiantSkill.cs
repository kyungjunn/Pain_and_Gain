using UnityEngine;

public class GiantSkill : AugmentSkill
{
    [SerializeField] private float scaleMultiplier = 1.5f;

    protected override void OnApply()
    {
        if (Player != null)
        {
            Player.transform.localScale *= scaleMultiplier;
        }
    }

    protected override void OnRemove()
    {
        if (Player != null)
        {
            Player.transform.localScale /= scaleMultiplier;
        }
    }

    private void OnValidate()
    {
        scaleMultiplier = Mathf.Max(0.01f, scaleMultiplier);
    }
}
