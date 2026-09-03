using UnityEngine;

// 테스트용 스킬 증강: 플레이어 거대화. 원복 검증이 가장 쉬운 스킬.
// 곱하고 나누는 방식이라 같은 스킬이 여러 개여도 제거 순서와 무관하게 원복된다.
public class GiantSkill : AugmentSkill
{
    [SerializeField] private float scaleMultiplier = 1.5f;

    protected override void OnApply()
    {
        Player.transform.localScale *= scaleMultiplier;
    }

    protected override void OnRemove()
    {
        Player.transform.localScale /= scaleMultiplier;
    }

    private void OnValidate()
    {
        scaleMultiplier = Mathf.Max(0.01f, scaleMultiplier);
    }
}
