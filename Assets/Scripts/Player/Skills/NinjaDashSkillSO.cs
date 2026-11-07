using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// E 돌진: 정적 지형 사전 검사, 이동 구간 내 적 1회 타격.
[CreateAssetMenu(menuName = "Player/Skills/Ninja Dash")]
public sealed class NinjaDashSkillSO : PlayerSkillSO
{
    [SerializeField] private AudioClip dashStartSfx;
    [SerializeField, Min(0.1f)] private float distance = 7f;
    [SerializeField, Min(0.05f)] private float duration = 0.28f;
    [SerializeField, Min(0.1f)] private float hitRadius = 1.1f;
    [SerializeField, Min(0f)] private float damageMultiplier = 2f;
    [SerializeField] private LayerMask enemyLayers = ~0;

    public override bool Cast(PlayerSkillController owner, PlayerDamageType damageType)
    {
        // 피해 계산에 필요한 참조 확인 후 돌진 코루틴 시작.
        if (owner == null || owner.DamageDealer == null || owner.Stats == null || owner.IsPlayerDead)
            return false;

        PlayerAugments playerAugments = owner.GetComponent<PlayerAugments>();
        float damageBonus = playerAugments != null
            ? playerAugments.GetCombatAugmentValue(PlayerCombatAugmentEffect.SkillDamage, this)
            : 0f;
        float effectiveDamageMultiplier = damageMultiplier * Mathf.Max(0f, 1f + damageBonus);

        owner.StartCoroutine(Dash(owner, damageType, effectiveDamageMultiplier));
        return true;
    }

    private IEnumerator Dash(
        PlayerSkillController owner,
        PlayerDamageType damageType,
        float effectiveDamageMultiplier)
    {
        // 스킬 피해 확정, 이미 맞은 적 기록.
        int damage = Mathf.Max(1, Mathf.RoundToInt(owner.Stats.AttackDamage * effectiveDamageMultiplier));
        var hitEnemies = new HashSet<EnemyHealth>();
        Vector3 direction = owner.transform.forward;
        direction.y = 0f;
        direction.Normalize();

        // 시작 시 한 번 지형 충돌 거리 측정.
        float allowedDistance = SkillDashMovement.GetTerrainLimitedDistance(owner.transform, direction, distance);

        // 돌진 중 일반 이동 잠금. 종료/중단 시 반드시 해제.
        bool movementLockBeforeDash = owner.IsMovementLocked;
        owner.SetMovementLocked(true);
        try
        {
            if (owner.isActiveAndEnabled && !owner.IsPlayerDead && dashStartSfx != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(dashStartSfx);

            yield return SkillDashMovement.Move(owner.transform, direction, allowedDistance, duration,
                (start, end, traveled) =>
                {
                    // 이번 프레임 이동 구간의 적 판정. 적마다 중복 피해 방지.
                    foreach (Collider hit in Physics.OverlapCapsule(start, end, hitRadius, enemyLayers,
                                 QueryTriggerInteraction.Collide))
                    {
                        EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();
                        if (enemy != null && hitEnemies.Add(enemy))
                            owner.DamageDealer.DealDamage(enemy, damage, damageType);
                    }
                },
                () => GameManager.Instance != null && GameManager.Instance.IsPaused,
                () => owner == null || !owner.isActiveAndEnabled || owner.IsPlayerDead);

            // 중단·사망·비활성화가 아니면 허용된 거리만큼 이동한 돌진을 완료로 본다.
            if (allowedDistance > 0f && owner != null && owner.isActiveAndEnabled && !owner.IsPlayerDead)
                owner.NotifyDashCompleted(this);
        }
        finally
        {
            if (owner != null)
                owner.SetMovementLocked(owner.IsPlayerDead ? false : movementLockBeforeDash);
        }
    }

    private void OnValidate()
    {
        distance = Mathf.Max(0.1f, distance);
        duration = Mathf.Max(0.05f, duration);
        hitRadius = Mathf.Max(0.1f, hitRadius);
        damageMultiplier = Mathf.Max(0f, damageMultiplier);
    }
}
