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
    private PlayerAugments playerAugments;
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
        playerAugments = GetComponent<PlayerAugments>();
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

        int damage = Mathf.Max(1, Mathf.RoundToInt(AttackDamage * damageMultiplier));
        int extraProjectileCount = 0;
        float spreadDegrees = 0f;

        if (playerAugments != null)
        {
            extraProjectileCount = Mathf.Max(0, Mathf.RoundToInt(
                playerAugments.GetCombatAugmentValue(PlayerCombatAugmentEffect.BasicProjectileCount)));
            spreadDegrees = Mathf.Max(0f, playerAugments.GetCombatAugmentSpread(
                PlayerCombatAugmentEffect.BasicProjectileCount));
        }

        int projectileCount = 1 + extraProjectileCount;
        for (int i = 0; i < projectileCount; i++)
        {
            float yaw = projectileCount > 1
                ? Mathf.Lerp(-spreadDegrees * 0.5f, spreadDegrees * 0.5f,
                    i / (float)(projectileCount - 1))
                : 0f;
            Quaternion yawRotation = Quaternion.AngleAxis(yaw, projectileOrigin.up);
            Quaternion projectileRotation = yawRotation * projectileOrigin.rotation *
                projectilePrefab.transform.localRotation;
            Vector3 direction = yawRotation * projectileOrigin.forward;

            DamageProjectile projectile = Instantiate(
                projectilePrefab,
                projectileOrigin.position,
                projectileRotation);
            projectile.Initialize(
                damageDealer,
                PlayerDamageType.BasicAttack,
                damage,
                speed,
                maxDistance,
                direction);
        }

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
