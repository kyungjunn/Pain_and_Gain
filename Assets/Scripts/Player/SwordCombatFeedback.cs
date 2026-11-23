using UnityEngine;

// 전사 기본 공격 전용 피격 연출.
// 타격 판정 스크립트가 실제 접촉 위치를 전달하므로 이펙트가 정확한 위치에서 발생한다.
public class SwordCombatFeedback : MonoBehaviour
{
    [Header("Hit VFX")]
    [SerializeField] private ParticleSystem hitVfx; // 기존 SwordHitVFX 백업용
    [Tooltip("추천: Sparks explode white / blue. 큰 Shockwave는 평타에는 비추천.")]
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField, Min(0.01f)] private float hitVfxScale = 0.38f;
    [SerializeField, Min(0.1f)] private float hitVfxLifetime = 0.9f;
    [SerializeField, Min(0f)] private float hitVfxInterval = 0.05f;

    private float nextHitVfxTime;

    // MeleePlayerAttack에서 실제로 적중했을 때만 호출.
    public void PlayMeleeImpact(Vector3 contactPoint)
    {
        if (!isActiveAndEnabled) return;

        if (hitVfxPrefab != null)
        {
            SpawnImpactVfx(contactPoint);
        }
        else if (hitVfx != null && Time.unscaledTime >= nextHitVfxTime)
        {
            nextHitVfxTime = Time.unscaledTime + hitVfxInterval;
            hitVfx.Play(true);
        }

        // EnemyHealth가 이미 피격음과 빨간 피격 플래시를 처리하므로
        // 여기서는 같은 효과음을 중복 재생하지 않는다.
    }

    private void SpawnImpactVfx(Vector3 contactPoint)
    {
        GameObject vfx = Instantiate(
            hitVfxPrefab, contactPoint, hitVfxPrefab.transform.rotation);
        vfx.transform.localScale *= hitVfxScale;

        ParticleSystem[] particles = vfx.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particle in particles)
        {
            var main = particle.main;
            main.loop = false;
            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        foreach (ParticleSystem particle in particles)
            particle.Play(false);

        Destroy(vfx, hitVfxLifetime);
    }

    private void OnValidate()
    {
        hitVfxScale = Mathf.Max(0.01f, hitVfxScale);
        hitVfxLifetime = Mathf.Max(0.1f, hitVfxLifetime);
        hitVfxInterval = Mathf.Max(0f, hitVfxInterval);
    }
}
