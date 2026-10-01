using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Player/Skills/Ninja Dash")]
public sealed class NinjaDashSkillSO : PlayerSkillSO
{
    [SerializeField, Min(0.1f)] private float distance = 7f;
    [SerializeField, Min(0.05f)] private float duration = 0.28f;
    [SerializeField, Min(0.1f)] private float hitRadius = 1.1f;
    [SerializeField, Min(0f)] private float damageMultiplier = 2f;
    [SerializeField] private LayerMask enemyLayers = ~0;

    public override bool Cast(PlayerSkillController owner, PlayerDamageType damageType)
    {
        if (owner == null || owner.DamageDealer == null || owner.Stats == null)
            return false;

        owner.StartCoroutine(Dash(owner, damageType));
        return true;
    }

    private IEnumerator Dash(PlayerSkillController owner, PlayerDamageType damageType)
    {
        int damage = Mathf.Max(1, Mathf.RoundToInt(owner.Stats.AttackDamage * damageMultiplier));
        var hitEnemies = new HashSet<EnemyHealth>();

        owner.SetMovementLocked(true);
        try
        {
            yield return SkillDashMovement.Move(owner.transform, owner.transform.forward, distance, duration,
                (start, end, traveled) =>
                {
                foreach (Collider hit in Physics.OverlapCapsule(start, end, hitRadius, enemyLayers,
                             QueryTriggerInteraction.Collide))
                {
                    EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();
                    if (enemy != null && hitEnemies.Add(enemy))
                        owner.DamageDealer.DealDamage(enemy, damage, damageType);
                }
                });
        }
        finally
        {
            if (owner != null)
                owner.SetMovementLocked(false);
        }
    }

    private void OnValidate()
    {
        distance = Mathf.Max(0.1f, distance);
        duration = Mathf.Max(0.05f, duration);
        hitRadius = Mathf.Max(0.1f, hitRadius);
        damageMultiplier = Mathf.Max(0f, damageMultiplier);
    }
}
