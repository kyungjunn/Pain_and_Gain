using System;
using UnityEngine;

// 플레이어 현재 체력, 피격, 회복을 관리
public class PlayerHealth : LivingEntity
{
    [SerializeField] private PlayerStats stats;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private bool disableControlsOnDeath;

    public int MaxHealth => stats != null ? Mathf.RoundToInt(stats.HP) : maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    public Action onHealthChanged;

    private PlayerStateManager stateManager;
    private Animator animator;
    private Rigidbody rb;

    private void Awake()
    {
        if (stats == null)
        {
            stats = GetComponent<PlayerStats>();
        }

        stateManager = GetComponent<PlayerStateManager>();
        animator = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody>();

        CurrentHealth = MaxHealth;
    }

    private void OnEnable()
    {
        if (stats != null)
        {
            stats.onStatsChanged += HandleStatsChanged;
        }
    }

    private void OnDisable()
    {
        if (stats != null)
        {
            stats.onStatsChanged -= HandleStatsChanged;
        }
    }

    public override void TakeDamage(int damage)
    {
        if (IsDead || damage <= 0)
        {
            return;
        }

        // 방어력을 적용하되 최소 1 피해는 보장
        int finalDamage = Mathf.Max(1, damage - Defense);
        CurrentHealth = Mathf.Max(CurrentHealth - finalDamage, 0);
        onHealthChanged?.Invoke();

        if (CurrentHealth == 0)
        {
            Die();
        }
    }

    public int Heal(int amount)
    {
        if (IsDead || amount <= 0 || CurrentHealth >= MaxHealth)
        {
            return 0;
        }

        int previousHealth = CurrentHealth;
        CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
        onHealthChanged?.Invoke();
        return CurrentHealth - previousHealth;
    }

    private int Defense => stats != null ? Mathf.RoundToInt(stats.Defense) : 0;

    private void HandleStatsChanged()
    {
        int newMaxHealth = MaxHealth;
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0, newMaxHealth);
        onHealthChanged?.Invoke();
    }

    private void Die()
    {
        if (IsDead)
        {
            return;
        }

        IsDead = true;

        stateManager?.ChangeState(PlayerState.Dead);

        if (rb != null)
        {
            rb.linearVelocity =
                new Vector3(0f, rb.linearVelocity.y, 0f);
        }

        if (animator != null)
        {
            if (animator.layerCount > 1)
            {
                animator.SetLayerWeight(1, 0f);
            }

            animator.SetTrigger("Die");
        }

        if (!disableControlsOnDeath)
        {
            return;
        }

        if (TryGetComponent(out PlayerController controller))
        {
            controller.enabled = false;
        }

        if (TryGetComponent(out PlayerMovement movement))
        {
            movement.enabled = false;
        }
    }
}
