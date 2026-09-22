using UnityEngine;

[RequireComponent(typeof(PlayerDamageDealer))]
public class ProjectilePlayerAttack : MonoBehaviour, IPlayerBasicAttack
{
    [SerializeField] private PlayerStats stats;
    [SerializeField] private DamageProjectile projectilePrefab;
    [SerializeField] private Transform projectileOrigin;
    [SerializeField] private float speed = 14f;
    [SerializeField] private float maxDistance = 14f;
    [SerializeField] private float damageMultiplier = 1f;
    [SerializeField] private float attackCooldown = 0.5f;

    private PlayerDamageDealer damageDealer;
    private float nextAttackTime;

    private float AttackDamage => stats != null ? stats.AttackDamage : 10f;
    private float AttackCooldown => stats != null && stats.AttackSpeed > 0f ? 1f / stats.AttackSpeed : attackCooldown;

    private void Awake()
    {
        if (stats == null)
        {
            stats = GetComponent<PlayerStats>();
        }

        damageDealer = GetComponent<PlayerDamageDealer>();
    }

    public bool TryAttack()
    {
        if (projectilePrefab == null || projectileOrigin == null || damageDealer == null)
        {
            return false;
        }

        if (Time.time < nextAttackTime)
        {
            return false;
        }

        DamageProjectile projectile = Instantiate(
            projectilePrefab,
            projectileOrigin.position,
            projectileOrigin.rotation * projectilePrefab.transform.localRotation);

        int damage = Mathf.Max(1, Mathf.RoundToInt(AttackDamage * damageMultiplier));
        projectile.Initialize(
            damageDealer,
            PlayerDamageType.BasicAttack,
            damage,
            speed,
            maxDistance,
            projectileOrigin.forward);

        nextAttackTime = Time.time + AttackCooldown;
        return true;
    }

    private void OnValidate()
    {
        speed = Mathf.Max(0.1f, speed);
        maxDistance = Mathf.Max(0.1f, maxDistance);
        damageMultiplier = Mathf.Max(0.1f, damageMultiplier);
        attackCooldown = Mathf.Max(0.1f, attackCooldown);
    }
}
