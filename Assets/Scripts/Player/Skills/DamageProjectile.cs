// 공용 투사체: 관통 직격·단발 범위 폭발
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class DamageProjectile : MonoBehaviour
{
    [Header("Launch height correction")]
    [SerializeField, Range(0f, 30f)] private float horizontalAimAngle = 8f;
    [SerializeField, Range(0f, 60f)] private float verticalAimAngle = 35f;
    [Tooltip("비행 중 높이 추적 속도(도/초). 좌우 방향은 추적하지 않는다.")]
    [SerializeField, Min(0f)] private float verticalTrackingSpeed = 360f;
    private static readonly ProjectileHeightAim HeightAim = new ProjectileHeightAim();

    private PlayerDamageDealer damageDealer;
    private PlayerDamageType damageType;
    private Vector3 spawnPosition;
    private float maxDistance;
    private float moveSpeed;
    private int damage;
    private Vector3 moveDirection;
    private Vector3 planarDirection;
    private Collider aimTarget;
    private EnemyHealth aimTargetHealth;
    private bool hadAimTarget;
    private Rigidbody body;
    private bool initialized;
    private float explosionRadius;
    private ParticleSystem[] particles;
    private WizardProjectileVisual wizardProjectileVisual;
    private int remainingPierces;
    private readonly HashSet<EnemyHealth> directHits = new HashSet<EnemyHealth>();
    private readonly SkillAreaQuery explosionQuery = new SkillAreaQuery();

    // 발사 설정
    public void Initialize(PlayerDamageDealer dealer, PlayerDamageType type, int value,
        float speed, float distance, Vector3 direction, float blastRadius = 0f, float visualScale = 1f,
        int additionalPierces = 0)
    {
        damageDealer = dealer;
        damageType = type;
        damage = Mathf.Max(1, value);
        maxDistance = Mathf.Max(0.1f, distance);
        moveSpeed = Mathf.Max(0f, speed);
        moveDirection = HeightAim.Resolve(transform.position, direction, maxDistance,
            dealer != null ? dealer.transform : null, horizontalAimAngle, verticalAimAngle, out aimTarget);
        aimTargetHealth = aimTarget != null ? aimTarget.GetComponentInParent<EnemyHealth>() : null;
        hadAimTarget = aimTarget != null;
        planarDirection = Vector3.ProjectOnPlane(moveDirection, Vector3.up).normalized;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.FromToRotation(direction.normalized, moveDirection) * transform.rotation;
        spawnPosition = transform.position;
        explosionRadius = Mathf.Max(0f, blastRadius);
        remainingPierces = Mathf.Max(0, additionalPierces);
        directHits.Clear();
        // 연출만 확대: 충돌체 확대에 따른 조기 적중 방지
        foreach (Transform child in transform)
            child.localScale *= Mathf.Max(0.1f, visualScale);
        if (explosionRadius > 0f || !Mathf.Approximately(visualScale, 1f))
        {
            particles = GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in particles)
            {
                var main = particle.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }
        wizardProjectileVisual = GetComponent<WizardProjectileVisual>();
        initialized = true;

        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    // 이동 / 사거리 종료
    private void FixedUpdate()
    {
        if (!initialized)
            return;

        float remaining = maxDistance - Vector3.Distance(body.position, spawnPosition);
        if (remaining <= 0.001f)
        {
            Finish(null, Vector3.zero);
            return;
        }

        TrackTargetHeight(remaining);
        float step = Mathf.Min(moveSpeed * Time.fixedDeltaTime, remaining);
        body.MovePosition(body.position + moveDirection * step);
    }

    // 비행 중 높이만 보정: 수평 진행 방향은 플레이어 조준 그대로 유지
    private void TrackTargetHeight(float remaining)
    {
        if (verticalTrackingSpeed <= 0f || planarDirection.sqrMagnitude < 0.001f || !hadAimTarget)
            return;

        if (aimTarget == null || !aimTarget.enabled || aimTargetHealth == null || aimTargetHealth.IsDead)
        {
            // 추적 대상이 사라진 경우, 같은 수평 방향 콘 안에서 재탐색
            HeightAim.Resolve(body.position, planarDirection, remaining,
                damageDealer != null ? damageDealer.transform : null,
                horizontalAimAngle, verticalAimAngle, out aimTarget);
            aimTargetHealth = aimTarget != null ? aimTarget.GetComponentInParent<EnemyHealth>() : null;
            if (aimTarget == null)
            {
                hadAimTarget = false;
                return;
            }
        }

        Vector3 offset = aimTarget.bounds.center - body.position;
        float along = Vector3.Dot(offset, planarDirection);
        if (along <= 0.05f)
            return;

        float desiredPitch = Mathf.Clamp(Mathf.Atan2(offset.y, along) * Mathf.Rad2Deg,
            -verticalAimAngle, verticalAimAngle);
        float currentPitch = Mathf.Asin(Mathf.Clamp(moveDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
        float pitch = Mathf.MoveTowards(currentPitch, desiredPitch, verticalTrackingSpeed * Time.fixedDeltaTime);
        if (Mathf.Approximately(pitch, currentPitch))
            return;

        float radians = pitch * Mathf.Deg2Rad;
        Vector3 next = planarDirection * Mathf.Cos(radians) + Vector3.up * Mathf.Sin(radians);
        body.MoveRotation(Quaternion.FromToRotation(moveDirection, next) * body.rotation);
        moveDirection = next;
    }

    // 적 적중
    private void OnTriggerEnter(Collider other)
    {
        if (!initialized || damageDealer == null || other.transform.root == damageDealer.transform)
            return;

        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();
        if (enemy == null || enemy.IsDead || !directHits.Add(enemy))
            return;

        if (explosionRadius <= 0f && remainingPierces > 0)
        {
            remainingPierces--;
            damageDealer.DealDamage(enemy, damage, damageType, spawnPosition);
            wizardProjectileVisual?.OnSuccessfulHit(other.ClosestPoint(transform.position));
            return;
        }

        Finish(enemy, other.ClosestPoint(transform.position));
    }

    private void Finish(EnemyHealth directTarget, Vector3 hitPosition)
    {
        if (!initialized)
            return;

        initialized = false;
        if (explosionRadius <= 0f)
        {
            if (directTarget != null && damageDealer != null)
            {
                damageDealer.DealDamage(directTarget, damage, damageType, spawnPosition);
                wizardProjectileVisual?.OnSuccessfulHit(hitPosition, true);
            }
            Destroy(gameObject);
            return;
        }

        explosionQuery.DealDamage(damageDealer, body.position, explosionRadius, damage,
            damageType, spawnPosition, directTarget);
        if (directTarget != null)
            wizardProjectileVisual?.OnSuccessfulHit(hitPosition, true);

        foreach (Collider hitbox in GetComponentsInChildren<Collider>())
            hitbox.enabled = false;

        // 투사체 입자를 짧은 고정 폭발로 재사용
        foreach (ParticleSystem particle in particles)
        {
            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particle.main;
            main.loop = false;
            main.startLifetime = 0.4f;
            main.startSpeed = 3f;
            var emission = particle.emission;
            emission.enabled = false;
            particle.Play(false);
            particle.Emit(24);
        }
        Destroy(gameObject, 0.6f);
    }

}
