using UnityEngine;

[CreateAssetMenu(menuName = "Player Skills/Projectile Skill")]
public sealed class ProjectileSkillSO : PlayerSkillSO
{
    [SerializeField] private SkillProjectile projectilePrefab;
    [SerializeField, Min(0.1f)] private float speed = 12f;
    [SerializeField, Min(0.1f)] private float maxDistance = 15f;
    [SerializeField, Min(0.1f)] private float damageMultiplier = 1f;

    public override bool Cast(PlayerSkillController owner, PlayerDamageType damageType)
    {
        if (projectilePrefab == null || owner.SkillOrigin == null || owner.DamageDealer == null)
            return false;

        SkillProjectile projectile = Instantiate(
            projectilePrefab,
            owner.SkillOrigin.position,
            owner.SkillOrigin.rotation);

        float attackDamage = owner.Stats != null ? owner.Stats.AttackDamage : 10f;
        int damage = Mathf.Max(1, Mathf.RoundToInt(attackDamage * damageMultiplier));
        projectile.Initialize(owner.DamageDealer, damageType, damage, speed, maxDistance,
            owner.SkillOrigin.forward);
        return true;
    }
}
