using System.Collections.Generic;
using UnityEngine;

// 플레이어 전방 범위 안의 적을 찾아 광역 피해를 적용
public class PlayerAttack : MonoBehaviour
{
    [SerializeField] private PlayerStats stats;
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private float attackRange = 2.2f;
    [SerializeField] private float attackAngle = 90f;
    [SerializeField] private float attackCooldown = 0.5f;
    [SerializeField] private LayerMask enemyLayers = ~0;
    [SerializeField] private int maxTargets;
    [SerializeField] private float areaDamageMultiplier = 1f;
    [SerializeField] private Color attackGizmoColor = new Color(1f, 0.2f, 0.1f, 0.75f);
    [SerializeField] private int attackGizmoSegments = 24;

    private readonly List<EnemyHealth> attackTargets = new List<EnemyHealth>();
    private readonly HashSet<EnemyHealth> uniqueTargets = new HashSet<EnemyHealth>();
    private float nextAttackTime;

    private float AttackDamage => stats != null ? stats.AttackDamage : 10f;
    private float AttackCooldown => stats != null && stats.AttackSpeed > 0f ? 1f / stats.AttackSpeed : attackCooldown;

    private void Awake()
    {
        if (stats == null)
        {
            stats = GetComponent<PlayerStats>();
        }
    }

    public bool TryAttack()
    {
        if (Time.time < nextAttackTime)
        {
            return false;
        }

        nextAttackTime = Time.time + AttackCooldown;

        int targetCount = FindAttackTargets(attackTargets);

        if (targetCount == 0)
        {
            return false;
        }

        int damage = Mathf.Max(1, Mathf.RoundToInt(AttackDamage * areaDamageMultiplier));

        for (int i = 0; i < targetCount; i++)
        {
            attackTargets[i].TakeDamage(damage);
        }

        return true;
    }

    private int FindAttackTargets(List<EnemyHealth> results)
    {
        results.Clear();
        uniqueTargets.Clear();

        // OverlapSphere로 주변 적을 찾은 뒤 전방 각도 조건으로 실제 타격 대상을 좁힘
        Transform origin = attackOrigin != null ? attackOrigin : transform;
        Collider[] hits = Physics.OverlapSphere(origin.position, attackRange, enemyLayers, QueryTriggerInteraction.Ignore);

        foreach (Collider hit in hits)
        {
            EnemyHealth enemyHealth = hit.GetComponentInParent<EnemyHealth>();

            // 같은 적의 여러 콜라이더가 잡혀도 한 번만 피해를 주도록 중복 제거
            if (enemyHealth == null || enemyHealth.IsDead || !uniqueTargets.Add(enemyHealth))
            {
                continue;
            }

            Vector3 direction = enemyHealth.transform.position - origin.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > attackRange * attackRange)
            {
                continue;
            }

            if (direction.sqrMagnitude > 0.001f && Vector3.Angle(origin.forward, direction) > attackAngle * 0.5f)
            {
                continue;
            }

            results.Add(enemyHealth);

            if (maxTargets > 0 && results.Count >= maxTargets)
            {
                break;
            }
        }

        return results.Count;
    }

    private void OnValidate()
    {
        attackRange = Mathf.Max(0.1f, attackRange);
        attackAngle = Mathf.Clamp(attackAngle, 1f, 360f);
        attackCooldown = Mathf.Max(0.1f, attackCooldown);
        maxTargets = Mathf.Max(0, maxTargets);
        areaDamageMultiplier = Mathf.Max(0.1f, areaDamageMultiplier);
        attackGizmoSegments = Mathf.Max(4, attackGizmoSegments);
    }

    private void OnDrawGizmosSelected()
    {
        Transform origin = attackOrigin != null ? attackOrigin : transform;

        if (origin == null)
        {
            return;
        }

        Vector3 originPosition = origin.position;
        Vector3 forward = origin.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = transform.forward;
            forward.y = 0f;
        }

        forward.Normalize();

        Color previousColor = Gizmos.color;
        Gizmos.color = attackGizmoColor;

        // Scene 뷰에서 공격 범위를 확인하기 위한 디버그 표시
        Vector3 leftDirection = Quaternion.Euler(0f, -attackAngle * 0.5f, 0f) * forward;
        Vector3 rightDirection = Quaternion.Euler(0f, attackAngle * 0.5f, 0f) * forward;

        Gizmos.DrawLine(originPosition, originPosition + leftDirection * attackRange);
        Gizmos.DrawLine(originPosition, originPosition + rightDirection * attackRange);
        DrawAttackArc(originPosition, forward);

        Gizmos.color = previousColor;
    }

    private void DrawAttackArc(Vector3 originPosition, Vector3 forward)
    {
        int segmentCount = Mathf.Max(4, attackGizmoSegments);
        float startAngle = -attackAngle * 0.5f;
        float angleStep = attackAngle / segmentCount;
        Vector3 previousPoint = originPosition + (Quaternion.Euler(0f, startAngle, 0f) * forward) * attackRange;

        for (int i = 1; i <= segmentCount; i++)
        {
            float angle = startAngle + angleStep * i;
            Vector3 nextPoint = originPosition + (Quaternion.Euler(0f, angle, 0f) * forward) * attackRange;
            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }
    }
}
