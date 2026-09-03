using UnityEngine;

// 스킬 증강 하나의 실제 동작.
// 규칙은 두 개뿐이다.
// 1. OnApply에서 자기 효과를 켠다.
// 2. OnRemove에서 반드시 원복한다. OnDestroy가 자동으로 호출하므로 박탈은 Destroy 한 번으로 끝난다.
public abstract class AugmentSkill : MonoBehaviour
{
    // 효과 적용 대상 플레이어
    protected GameObject Player { get; private set; }

    // PlayerAugments가 Instantiate 직후 호출
    public void Apply(GameObject player)
    {
        Player = player;
        OnApply();
    }

    private void OnDestroy()
    {
        // 씬 언로드 등으로 플레이어가 먼저 사라진 경우는 원복할 대상이 없음
        if (Player != null)
        {
            OnRemove();
        }
    }

    // 효과 켜기
    protected abstract void OnApply();

    // OnApply의 원복. 구독 해제, 스케일 복구 등
    protected abstract void OnRemove();
}
