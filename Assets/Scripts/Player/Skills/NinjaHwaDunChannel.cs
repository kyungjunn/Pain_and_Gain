using System.Collections.Generic;
using DigitalRuby.PyroParticles;
using UnityEngine;

public sealed class NinjaHwaDunChannel : MonoBehaviour
{
    private readonly List<EnemyHealth> hitEnemies = new List<EnemyHealth>();

    private PlayerSkillController owner;
    private PlayerSkillSO skill;
    private PlayerDamageDealer damageDealer;
    private FireAuraHitbox hitbox;
    private float tickInterval;
    private float endTime;
    private float nextTickTime;
    private int damage;
    private bool stopped;

    public void Initialize(
        PlayerSkillController skillOwner,
        PlayerSkillSO channelSkill,
        int tickDamage,
        float interval,
        float channelDuration)
    {
        owner = skillOwner;
        skill = channelSkill;
        damageDealer = skillOwner != null ? skillOwner.DamageDealer : null;
        damage = Mathf.Max(1, tickDamage);
        tickInterval = Mathf.Max(0.05f, interval);
        endTime = Time.time + Mathf.Max(0.05f, channelDuration);
        nextTickTime = Time.time;
        hitbox = GetComponent<FireAuraHitbox>();
        if (hitbox == null)
            hitbox = gameObject.AddComponent<FireAuraHitbox>();

        IgnoreOwnerColliders();
    }

    private void Update()
    {
        if (stopped)
            return;

        if (owner == null || IsOwnerDead())
        {
            StopChannel(false);
            return;
        }

        if (Time.time >= endTime)
        {
            StopChannel(true);
            return;
        }

        if (Time.time < nextTickTime)
            return;

        nextTickTime = Time.time + tickInterval;
        ApplyTickDamage();
    }

    private void StopChannel(bool notifyOwner)
    {
        if (stopped)
            return;

        stopped = true;
        enabled = false;

        Collider collider = GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;

        FireBaseScript fire = GetComponent<FireBaseScript>();
        if (fire != null)
            fire.Stop();
        else
            Destroy(gameObject);

        if (notifyOwner && owner != null)
            owner.NotifyChannelFinished(skill);
    }
    private bool IsOwnerDead()
    {
        PlayerStateManager stateManager = owner != null ? owner.GetComponent<PlayerStateManager>() : null;
        return stateManager != null && stateManager.CurrentState == PlayerState.Dead;
    }

    private void IgnoreOwnerColliders()
    {
        Collider self = GetComponent<Collider>();
        if (self == null || owner == null)
            return;

        Collider[] ownerColliders = owner.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < ownerColliders.Length; i++)
        {
            if (ownerColliders[i] != null && ownerColliders[i] != self)
                Physics.IgnoreCollision(self, ownerColliders[i], true);
        }
    }

    private void ApplyTickDamage()
    {
        if (hitbox == null || damageDealer == null)
            return;

        hitbox.GetEnemies(hitEnemies);
        for (int i = 0; i < hitEnemies.Count; i++)
        {
            EnemyHealth enemy = hitEnemies[i];
            if (enemy != null)
                damageDealer.DealDamage(enemy, damage, PlayerDamageType.Skill);
        }
    }
}
