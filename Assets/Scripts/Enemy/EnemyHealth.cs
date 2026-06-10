using System;
using UnityEngine;
using UnityEngine.AI;

// 적의 체력, 사망 처리, 경험치 지급을 관리
public class EnemyHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private EnemyStats stats;
    [SerializeField] private int maxHealth = 20;
    [SerializeField] private int expReward = 1000;
    [SerializeField] private float destroyDelay = 2f;

    public int MaxHealth => stats != null ? stats.MaxHealth : maxHealth;
    public int ExpReward => stats != null ? stats.ExpReward : expReward;
    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    public float HealthPercent => MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 0f;

    public event Action<int, int> OnHealthChanged;

    private EnemyAnimator enemyAnimator;
    private EnemyHitFeedback hitFeedback;
    private bool expAwarded;
    private bool deathHandled;

    private void Awake()
    {
        enemyAnimator = GetComponent<EnemyAnimator>();
        hitFeedback = GetComponent<EnemyHitFeedback>();

        if (hitFeedback == null)
        {
            hitFeedback = gameObject.AddComponent<EnemyHitFeedback>();
        }

        CurrentHealth = MaxHealth;
    }

    // 플레이어 공격 등 외부 피해를 받을 때 호출
    public void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0)
        {
            return;
        }

        CurrentHealth = Mathf.Max(CurrentHealth - damage, 0);
        OnHealthChanged?.Invoke(CurrentHealth, MaxHealth);
        hitFeedback?.Play();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayEnemyHit();
        }

        if (CurrentHealth == 0)
        {
            Die();
            return;
        }

        enemyAnimator?.PlayHit();
    }

    // 사망 처리는 한 번만 실행해서 경험치 중복 지급을 막음
    private void Die()
    {
        if (deathHandled)
        {
            return;
        }

        deathHandled = true;
        IsDead = true;
        AwardExpOnce();
        enemyAnimator?.PlayDeath();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayEnemyDeath();
        }

        if (TryGetComponent(out NavMeshAgent agent))
        {
            agent.enabled = false;
        }

        foreach (Collider enemyCollider in GetComponentsInChildren<Collider>())
        {
            enemyCollider.enabled = false;
        }

        Destroy(gameObject, destroyDelay);
    }

    // 처치 보상 경험치를 플레이어 레벨 시스템에 1회 지급
    private void AwardExpOnce()
    {
        if (expAwarded || ExpReward <= 0)
        {
            return;
        }

        expAwarded = true;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject == null)
        {
            return;
        }

        if (playerObject.TryGetComponent(out PlayerLevelSystem levelSystem))
        {
            levelSystem.AddExp(ExpReward);
        }
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        expReward = Mathf.Max(0, expReward);
        destroyDelay = Mathf.Max(0f, destroyDelay);
    }
}
