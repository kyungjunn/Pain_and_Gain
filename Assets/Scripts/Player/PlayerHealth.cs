using System;
using UnityEngine;

// 플레이어 현재 체력, 피격, 레벨업 회복을 관리
public class PlayerHealth : LivingEntity
{
    [SerializeField] private PlayerStats stats;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private bool disableControlsOnDeath;

    public int MaxHealth => stats != null ? Mathf.RoundToInt(stats.HP) : maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    public Action onHealthChanged;

    private int cachedMaxHealth;
    private PlayerLevelSystem levelSystem;

    private void Awake()
    {
        if (stats == null)
        {
            stats = GetComponent<PlayerStats>();
        }

        levelSystem = GetComponent<PlayerLevelSystem>();
        cachedMaxHealth = MaxHealth;
        CurrentHealth = cachedMaxHealth;
    }

    private void OnEnable()
    {
        if (stats != null)
        {
            stats.onStatsChanged += HandleStatsChanged;
        }

        if (levelSystem == null)
        {
            levelSystem = GetComponent<PlayerLevelSystem>();
        }

        if (levelSystem != null)
        {
            levelSystem.onLevelUp += HandleLevelUp;
        }
    }

    private void OnDisable()
    {
        if (stats != null)
        {
            stats.onStatsChanged -= HandleStatsChanged;
        }

        if (levelSystem != null)
        {
            levelSystem.onLevelUp -= HandleLevelUp;
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

    private int Defense => stats != null ? Mathf.RoundToInt(stats.Defense) : 0;

    private void HandleStatsChanged()
    {
        int newMaxHealth = MaxHealth;
        int healthDifference = newMaxHealth - cachedMaxHealth;

        // HP 증강으로 최대 체력이 늘어난 경우 늘어난 만큼 현재 체력도 보정
        if (healthDifference > 0)
        {
            CurrentHealth += healthDifference;
        }

        cachedMaxHealth = newMaxHealth;
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0, cachedMaxHealth);
        onHealthChanged?.Invoke();
    }

    private void HandleLevelUp()
    {
        // 레벨업 보상으로 즉시 최대 체력까지 회복
        cachedMaxHealth = MaxHealth;
        CurrentHealth = cachedMaxHealth;
        onHealthChanged?.Invoke();
    }

    private void Die()
    {
        IsDead = true;

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
