using System.Collections;
using UnityEngine;

// 적의 공격 범위, 쿨다운, 실제 피해 적용 타이밍을 관리
public class EnemyAttack : MonoBehaviour
{
    [SerializeField] private EnemyStats stats;
    [SerializeField] private int attackDamage = 10;
    [SerializeField] private float attackRange = 1.8f;
    [SerializeField] private float attackCooldown = 1.2f;
    [SerializeField] private float fallbackHitDelay = 0.45f;
    [SerializeField] private bool useFallbackHitDelay = true;

    private EnemyAnimator enemyAnimator;
    private Transform pendingTarget;
    private Coroutine fallbackHitRoutine;
    private float nextAttackTime;
    private bool pendingDamageApplied;

    public float AttackRange => stats != null ? stats.AttackRange : attackRange;
    private int AttackDamage => stats != null ? stats.AttackDamage : attackDamage;
    private float AttackCooldown => stats != null ? stats.AttackCooldown : attackCooldown;

    private void Awake()
    {
        enemyAnimator = GetComponent<EnemyAnimator>();
    }

    // 공격 가능 상태일 때 공격 애니메이션을 재생하고 피해 적용을 예약
    public bool TryAttack(Transform target)
    {
        if (target == null || Time.time < nextAttackTime)
        {
            return false;
        }

        if (!IsTargetInRange(target))
        {
            return false;
        }

        nextAttackTime = Time.time + AttackCooldown;
        pendingTarget = target;
        pendingDamageApplied = false;

        if (fallbackHitRoutine != null)
        {
            StopCoroutine(fallbackHitRoutine);
        }

        enemyAnimator?.PlayAttack();

        if (useFallbackHitDelay)
        {
            fallbackHitRoutine = StartCoroutine(ApplyDamageAfterFallbackDelay(target));
        }

        return true;
    }

    // 공격 애니메이션 이벤트에서 호출되는 실제 타격 지점
    public void ApplyAttackDamageFromAnimationEvent()
    {
        ApplyPendingAttackDamage();
    }

    // 다른 공격 클립에서 OnAttackHit 이름을 쓰는 경우를 위한 별칭
    public void OnAttackHit()
    {
        ApplyPendingAttackDamage();
    }

    // 애니메이션 이벤트가 없는 경우에도 일정 시간 뒤 피해가 들어가도록 하는 보조 처리
    private IEnumerator ApplyDamageAfterFallbackDelay(Transform expectedTarget)
    {
        yield return new WaitForSeconds(fallbackHitDelay);

        if (pendingTarget == expectedTarget)
        {
            ApplyPendingAttackDamage();
        }

        fallbackHitRoutine = null;
    }

    private void ApplyPendingAttackDamage()
    {
        if (pendingDamageApplied || pendingTarget == null)
        {
            return;
        }

        if (TryGetComponent(out EnemyHealth enemyHealth) && enemyHealth.IsDead)
        {
            ClearPendingAttack();
            return;
        }

        if (!IsTargetInRange(pendingTarget))
        {
            ClearPendingAttack();
            return;
        }

        // 플레이어 루트 또는 자식 오브젝트에 붙은 IDamageable을 찾아 피해 적용
        IDamageable damageable = FindDamageable(pendingTarget);
        damageable?.TakeDamage(AttackDamage);

        ClearPendingAttack();
    }

    private void ClearPendingAttack()
    {
        pendingDamageApplied = true;
        pendingTarget = null;
    }

    private bool IsTargetInRange(Transform target)
    {
        float currentAttackRange = AttackRange;
        return (target.position - transform.position).sqrMagnitude <= currentAttackRange * currentAttackRange;
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
        attackDamage = Mathf.Max(1, attackDamage);
        attackRange = Mathf.Max(0.1f, attackRange);
        attackCooldown = Mathf.Max(0.1f, attackCooldown);
        fallbackHitDelay = Mathf.Max(0f, fallbackHitDelay);
    }
}
