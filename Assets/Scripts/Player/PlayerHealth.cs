using System;
using UnityEngine;

// 플레이어 현재 체력, 피격, 회복을 관리
[RequireComponent(typeof(PlayerStateManager), typeof(Rigidbody))]
public class PlayerHealth : LivingEntity
{
    [SerializeField] private PlayerStats stats;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private bool disableControlsOnDeath;

    public int MaxHealth => stats != null ? Mathf.RoundToInt(stats.HP) : maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    public Action onHealthChanged;
    public event Action<int> OnDamaged;

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

        if (GetComponent<CombatDamageNumbers>() == null)
            gameObject.AddComponent<CombatDamageNumbers>();
        if (GetComponent<PlayerCombatFeedback>() == null)
            gameObject.AddComponent<PlayerCombatFeedback>();
    }

    private void OnEnable()
    {
        if (stats != null)
        {
            stats.onStatsChanged += HandleStatsChanged;
        }

        HandleStatsChanged();
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
        int previousHealth = CurrentHealth;
        int finalDamage = Mathf.Max(1, damage - Defense);
        CurrentHealth = Mathf.Max(CurrentHealth - finalDamage, 0);
        int actualDamage = previousHealth - CurrentHealth;
        onHealthChanged?.Invoke();

        if (actualDamage > 0)
            OnDamaged?.Invoke(actualDamage);

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
        int healed = CurrentHealth - previousHealth;
        onHealthChanged?.Invoke();
        if (healed > 0 && AudioManager.Instance != null)
            AudioManager.Instance.PlayHeal();
        return healed;
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

        Debug.Log($"[Health] Die 호출, stateManager 있음: {stateManager != null}");
        stateManager?.ChangeState(PlayerState.Dead);

        if (rb != null)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
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
