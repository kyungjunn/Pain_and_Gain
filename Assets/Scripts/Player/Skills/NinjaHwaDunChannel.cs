using System.Collections.Generic;
using DigitalRuby.PyroParticles;
using UnityEngine;

// 화둔 지속 관리: 틱 피해, 시전자 충돌 제외, 종료 알림.
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

    public bool Initialize(
        PlayerSkillController skillOwner,
        PlayerSkillSO channelSkill,
        int tickDamage,
        float interval,
        float channelDuration)
    {
        if (stopped || skillOwner == null || !skillOwner.TryOwnHwaDunChannel(this))
            return false;

        // 시전자/스킬 정보 및 피해 간격, 종료 시각 기록.
        owner = skillOwner;
        skill = channelSkill;
        damageDealer = skillOwner != null ? skillOwner.DamageDealer : null;
        damage = Mathf.Max(1, tickDamage);
        tickInterval = Mathf.Max(0.05f, interval);
        endTime = Time.time + Mathf.Max(0.05f, channelDuration);
        nextTickTime = Time.time;
        hitbox = GetComponent<FireAuraHitbox>();
        // 중첩 적 탐색용 히트박스가 없으면 생성.
        if (hitbox == null)
            hitbox = gameObject.AddComponent<FireAuraHitbox>();

        IgnoreOwnerColliders();
        return true;
    }

    private void Update()
    {
        // 종료 여부 → 시전자 생존 → 지속시간 → 다음 틱 순서로 검사.
        if (stopped)
            return;

        if (owner == null || !owner.isActiveAndEnabled || owner.IsPlayerDead)
        {
            StopChannel();
            return;
        }

        if (Time.time >= endTime)
        {
            StopChannel();
            return;
        }

        if (Time.time < nextTickTime)
            return;

        nextTickTime = Time.time + tickInterval;
        // 틱 간격마다 현재 범위 안의 적에게 피해.
        ApplyTickDamage();
    }

    private void OnDisable()
    {
        StopChannel();
    }

    private void OnDestroy()
    {
        StopChannel();
    }

    public void StopChannel()
    {
        // 중복 종료 방지 및 히트박스 비활성화.
        if (stopped)
            return;

        stopped = true;
        enabled = false;

        Collider collider = GetComponent<Collider>();
        if (collider != null)
            collider.enabled = false;
        if (hitbox != null)
            hitbox.enabled = false;
        hitEnemies.Clear();

        FireBaseScript fire = GetComponent<FireBaseScript>();
        // 화염 이펙트 정지. 이펙트 스크립트가 없으면 오브젝트 제거.
        // Stop은 활성 오브젝트에서 코루틴으로 잔여 파티클을 정리한다.
        if (fire != null && gameObject.activeInHierarchy)
            fire.Stop();
        else
            Destroy(gameObject);

        if (owner != null)
            owner.NotifyHwaDunChannelFinished(this, skill);
        owner = null;
        damageDealer = null;
    }

    private void IgnoreOwnerColliders()
    {
        // 화염 트리거와 시전자 콜라이더 간 충돌 무시.
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
        // 현재 히트박스에 잡힌 적을 조회하고 각 적에게 틱 피해.
        if (hitbox == null || damageDealer == null)
            return;

        hitbox.GetEnemies(hitEnemies);
        for (int i = 0; i < hitEnemies.Count; i++)
        {
            if (stopped || owner == null || !owner.isActiveAndEnabled || owner.IsPlayerDead)
                break;

            EnemyHealth enemy = hitEnemies[i];
            if (enemy != null)
                damageDealer.DealDamage(enemy, damage, PlayerDamageType.Skill, sourceSkill: skill);
        }
    }
}
