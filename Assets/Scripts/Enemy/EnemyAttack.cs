using UnityEngine;

// 적의 공격 범위와 쿨타임을 관리
public class EnemyAttack : MonoBehaviour
{
    [SerializeField] private EnemyStats stats;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackCooldown = 1.2f;

    private EnemyAnimator enemyAnimator;
    private float nextAttackTime;

    public float AttackRange => stats != null ? stats.AttackRange : attackRange;
    private float AttackCooldown => stats != null ? stats.AttackCooldown : attackCooldown;

    private void Awake()
    {
        enemyAnimator = GetComponent<EnemyAnimator>();
    }

    // 공격 가능한 거리와 쿨타임이면 대상에게 피해 적용
    public bool TryAttack(Transform target)
    {
        if (target == null || Time.time < nextAttackTime)
        {
            return false;
        }

        float currentAttackRange = AttackRange;

        if ((target.position - transform.position).sqrMagnitude > currentAttackRange * currentAttackRange)
        {
            return false;
        }

        nextAttackTime = Time.time + AttackCooldown;
        enemyAnimator?.PlayAttack();

        IDamageable damageable = FindDamageable(target);
        damageable?.TakeDamage();
        return true;
    }

    private IDamageable FindDamageable(Transform target)
    {
        IDamageable damageable = target.GetComponentInParent<IDamageable>();

        if (damageable != null)
        {
            return damageable;
        }

        return target.GetComponentInChildren<IDamageable>();
    }

    private void OnValidate()
    {
        attackRange = Mathf.Max(0.1f, attackRange);
        attackCooldown = Mathf.Max(0.1f, attackCooldown);
    }
}
