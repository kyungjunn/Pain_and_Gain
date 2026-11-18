// 공용 투사체: 관통 직격·단발 범위 폭발
using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class DamageProjectile : MonoBehaviour
{
    private enum LeasePhase
    {
        Idle,
        Preparing,
        Flying,
        Resolving,
        Finishing,
        ReturnPending,
        Cancelled,
        Disposed
    }

    private struct ParticleSnapshot
    {
        public ParticleSystem Particle;
        public ParticleSystemScalingMode ScalingMode;
        public bool Loop;
        public bool PlayOnAwake;
        public ParticleSystem.MinMaxCurve StartLifetime;
        public float StartLifetimeMultiplier;
        public ParticleSystem.MinMaxCurve StartSpeed;
        public ParticleSystem.MinMaxCurve StartSize;
        public float StartSizeMultiplier;
        public int MaxParticles;
        public ParticleSystem.MinMaxGradient StartColor;
        public bool ColorOverLifetimeEnabled;
        public ParticleSystem.MinMaxGradient ColorOverLifetime;
        public bool EmissionEnabled;
        public ParticleSystem.MinMaxCurve RateOverTime;
        public float RateOverTimeMultiplier;

        public static ParticleSnapshot Capture(ParticleSystem particle)
        {
            ParticleSystem.MainModule main = particle.main;
            ParticleSystem.ColorOverLifetimeModule color = particle.colorOverLifetime;
            ParticleSystem.EmissionModule emission = particle.emission;
            return new ParticleSnapshot
            {
                Particle = particle,
                ScalingMode = main.scalingMode,
                Loop = main.loop,
                PlayOnAwake = main.playOnAwake,
                StartLifetime = CopyCurve(main.startLifetime),
                StartLifetimeMultiplier = main.startLifetimeMultiplier,
                StartSpeed = CopyCurve(main.startSpeed),
                StartSize = CopyCurve(main.startSize),
                StartSizeMultiplier = main.startSizeMultiplier,
                MaxParticles = main.maxParticles,
                StartColor = CopyGradient(main.startColor),
                ColorOverLifetimeEnabled = color.enabled,
                ColorOverLifetime = CopyGradient(color.color),
                EmissionEnabled = emission.enabled,
                RateOverTime = CopyCurve(emission.rateOverTime),
                RateOverTimeMultiplier = emission.rateOverTimeMultiplier
            };
        }

        public void Restore(bool suppressPlayOnAwake)
        {
            if (Particle == null)
                return;

            ParticleSystem.MainModule main = Particle.main;
            main.scalingMode = ScalingMode;
            main.loop = Loop;
            main.playOnAwake = suppressPlayOnAwake ? false : PlayOnAwake;
            main.startLifetime = StartLifetime;
            main.startLifetimeMultiplier = StartLifetimeMultiplier;
            main.startSpeed = StartSpeed;
            main.startSize = StartSize;
            main.startSizeMultiplier = StartSizeMultiplier;
            main.maxParticles = MaxParticles;
            main.startColor = StartColor;

            ParticleSystem.ColorOverLifetimeModule color = Particle.colorOverLifetime;
            color.enabled = ColorOverLifetimeEnabled;
            color.color = ColorOverLifetime;

            ParticleSystem.EmissionModule emission = Particle.emission;
            emission.enabled = EmissionEnabled;
            emission.rateOverTime = RateOverTime;
            emission.rateOverTimeMultiplier = RateOverTimeMultiplier;
        }
    }

    // Unity getters expose native-backed curves/gradients; a struct copy is not an immutable baseline.
    private static ParticleSystem.MinMaxCurve CopyCurve(ParticleSystem.MinMaxCurve value)
    {
        if (value.curveMin != null)
            value.curveMin = CopyAnimationCurve(value.curveMin);
        if (value.curveMax != null)
            value.curveMax = CopyAnimationCurve(value.curveMax);
        return value;
    }

    private static AnimationCurve CopyAnimationCurve(AnimationCurve value)
    {
        return new AnimationCurve(value.keys) { preWrapMode = value.preWrapMode, postWrapMode = value.postWrapMode };
    }

    private static ParticleSystem.MinMaxGradient CopyGradient(ParticleSystem.MinMaxGradient value)
    {
        if (value.gradientMin != null)
            value.gradientMin = CopyColorGradient(value.gradientMin);
        if (value.gradientMax != null)
            value.gradientMax = CopyColorGradient(value.gradientMax);
        return value;
    }

    private static Gradient CopyColorGradient(Gradient value)
    {
        var copy = new Gradient { mode = value.mode };
        copy.SetKeys(value.colorKeys, value.alphaKeys);
        return copy;
    }

    private struct ColliderSnapshot
    {
        public Collider Collider;
        public bool Enabled;
    }

    private struct RendererSnapshot
    {
        public Renderer Renderer;
        public bool Enabled;
    }

    [Header("Launch height correction")]
    [SerializeField, Range(0f, 30f)] private float horizontalAimAngle = 8f;
    [SerializeField, Range(0f, 60f)] private float verticalAimAngle = 35f;
    [Tooltip("비행 중 높이 추적 속도(도/초). 좌우 방향은 추적하지 않는다.")]
    [SerializeField, Min(0f)] private float verticalTrackingSpeed = 360f;

    private static readonly ProjectileHeightAim HeightAim = new ProjectileHeightAim();

    private readonly HashSet<EnemyHealth> directHits = new HashSet<EnemyHealth>();
    private readonly SkillAreaQuery explosionQuery = new SkillAreaQuery();

    private DamageProjectile poolPrefab;
    private PlayerProjectilePool poolOwner;
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
    private float explosionRadius;
    private int remainingPierces;
    private ProjectileVisualMode visualMode;
    private WizardProjectileVisual wizardProjectileVisual;

    private Transform[] originalImmediateChildren;
    private Vector3[] originalImmediateChildScales;
    private ParticleSystem[] sourceParticles;
    private ParticleSystem[] wizardExplosionParticles;
    private ParticleSnapshot[] sourceParticleSnapshots;
    private ColliderSnapshot[] colliderSnapshots;
    private RendererSnapshot[] rendererSnapshots;
    private Vector3 originalRootScale;
    private bool originalUseGravity;
    private bool originalIsKinematic;
    private bool originalDetectCollisions;
    private CollisionDetectionMode originalCollisionDetectionMode;
    private bool cacheBuilt;

    private LeasePhase phase = LeasePhase.Idle;
    private int ownerGeneration;
    private int resolutionDepth;
    private float finishDeadline;
    private float hitSparkDeadline;
    private int finishLeaseId;
    private int finishOwnerGeneration;

    public int LeaseId { get; private set; }
    public bool IsLeaseActive => phase == LeasePhase.Preparing || phase == LeasePhase.Flying ||
        phase == LeasePhase.Resolving || phase == LeasePhase.Finishing || phase == LeasePhase.ReturnPending;

    internal DamageProjectile PoolPrefab => poolPrefab;

    private void Awake()
    {
        EnsurePoolCache();
    }

    internal void InitializePoolObject(DamageProjectile prefab)
    {
        poolPrefab = prefab;
        EnsurePoolCache();
    }

    private void EnsurePoolCache()
    {
        if (cacheBuilt)
            return;

        body = GetComponent<Rigidbody>();
        if (body == null)
            throw new InvalidOperationException("DamageProjectile requires a Rigidbody.");

        originalRootScale = transform.localScale;
        int childCount = transform.childCount;
        originalImmediateChildren = new Transform[childCount];
        originalImmediateChildScales = new Vector3[childCount];
        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);
            originalImmediateChildren[i] = child;
            originalImmediateChildScales[i] = child.localScale;
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        colliderSnapshots = new ColliderSnapshot[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
            colliderSnapshots[i] = new ColliderSnapshot { Collider = colliders[i], Enabled = colliders[i].enabled };

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        rendererSnapshots = new RendererSnapshot[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            rendererSnapshots[i] = new RendererSnapshot { Renderer = renderers[i], Enabled = renderers[i].enabled };

        sourceParticles = GetComponentsInChildren<ParticleSystem>(true);
        sourceParticleSnapshots = new ParticleSnapshot[sourceParticles.Length];
        for (int i = 0; i < sourceParticles.Length; i++)
        {
            sourceParticleSnapshots[i] = ParticleSnapshot.Capture(sourceParticles[i]);
            sourceParticles[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = sourceParticles[i].main;
            main.playOnAwake = false;
        }

        wizardProjectileVisual = GetComponent<WizardProjectileVisual>();
        if (wizardProjectileVisual != null)
            wizardProjectileVisual.CacheSourceParticles(sourceParticles);

        originalUseGravity = body.useGravity;
        originalIsKinematic = body.isKinematic;
        originalDetectCollisions = body.detectCollisions;
        originalCollisionDetectionMode = body.collisionDetectionMode;
        cacheBuilt = true;
    }

    internal bool PrepareLease(PlayerProjectilePool owner, PlayerDamageDealer dealer, int capturedOwnerGeneration,
        Vector3 position, Quaternion rotation, PlayerDamageType type, int value, float speed, float distance,
        Vector3 direction, float blastRadius, float visualScale, int additionalPierces,
        ProjectileVisualMode requestedVisualMode)
    {
        EnsurePoolCache();
        if (phase != LeasePhase.Idle || owner == null || dealer == null)
            return false;

        LeaseId = unchecked(LeaseId + 1);
        if (LeaseId == 0)
            LeaseId = 1;

        phase = LeasePhase.Preparing;
        poolOwner = owner;
        damageDealer = dealer;
        ownerGeneration = capturedOwnerGeneration;
        damageType = type;
        damage = Mathf.Max(1, value);
        maxDistance = Mathf.Max(0.1f, distance);
        moveSpeed = Mathf.Max(0f, speed);
        explosionRadius = Mathf.Max(0f, blastRadius);
        remainingPierces = Mathf.Max(0, additionalPierces);
        visualMode = requestedVisualMode == ProjectileVisualMode.Basic ||
                     requestedVisualMode == ProjectileVisualMode.Empowered
            ? requestedVisualMode
            : ProjectileVisualMode.None;
        directHits.Clear();
        explosionQuery.ClearResults();
        finishDeadline = 0f;
        hitSparkDeadline = 0f;
        finishLeaseId = 0;
        finishOwnerGeneration = 0;

        RestoreOriginalState();
        if (visualMode != ProjectileVisualMode.None && wizardProjectileVisual == null)
        {
            wizardProjectileVisual = gameObject.AddComponent<WizardProjectileVisual>();
            wizardProjectileVisual.CacheSourceParticles(sourceParticles);
        }
        wizardProjectileVisual?.PrepareLease(visualMode);
        if (visualMode != ProjectileVisualMode.None && wizardExplosionParticles == null)
            wizardExplosionParticles = GetComponentsInChildren<ParticleSystem>(true);

        float scale = Mathf.Max(0.1f, visualScale);
        for (int i = 0; i < originalImmediateChildren.Length; i++)
        {
            Transform child = originalImmediateChildren[i];
            if (child != null)
                child.localScale = originalImmediateChildScales[i] * scale;
        }

        bool useHierarchyParticleScaling = explosionRadius > 0f || !Mathf.Approximately(scale, 1f);
        for (int i = 0; i < sourceParticleSnapshots.Length; i++)
        {
            ParticleSystem particle = sourceParticleSnapshots[i].Particle;
            if (particle != null)
                particle.scalingMode = useHierarchyParticleScaling
                    ? ParticleSystemScalingMode.Hierarchy
                    : sourceParticleSnapshots[i].ScalingMode;
        }
        wizardProjectileVisual?.ApplyLaunchScale(scale, useHierarchyParticleScaling, visualMode);

        moveDirection = HeightAim.Resolve(position, direction, maxDistance,
            dealer.transform, horizontalAimAngle, verticalAimAngle, out aimTarget);
        aimTargetHealth = aimTarget != null ? aimTarget.GetComponentInParent<EnemyHealth>() : null;
        hadAimTarget = aimTarget != null;
        planarDirection = Vector3.ProjectOnPlane(moveDirection, Vector3.up).normalized;
        Quaternion finalRotation = rotation;
        if (direction.sqrMagnitude > 0.001f)
            finalRotation = Quaternion.FromToRotation(direction.normalized, moveDirection) * rotation;

        transform.localScale = originalRootScale;
        transform.SetPositionAndRotation(position, finalRotation);
        body.useGravity = false;
        body.isKinematic = true;
        body.detectCollisions = originalDetectCollisions;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.position = position;
        body.rotation = finalRotation;
        spawnPosition = position;

        for (int i = 0; i < colliderSnapshots.Length; i++)
        {
            Collider collider = colliderSnapshots[i].Collider;
            if (collider != null)
                collider.enabled = colliderSnapshots[i].Enabled;
        }

        for (int i = 0; i < rendererSnapshots.Length; i++)
        {
            Renderer renderer = rendererSnapshots[i].Renderer;
            if (renderer != null)
                renderer.enabled = rendererSnapshots[i].Enabled;
        }

        phase = LeasePhase.Flying;
        gameObject.SetActive(true);

        for (int i = 0; i < sourceParticleSnapshots.Length; i++)
        {
            ParticleSystem particle = sourceParticleSnapshots[i].Particle;
            if (particle != null && (visualMode != ProjectileVisualMode.None || sourceParticleSnapshots[i].PlayOnAwake))
                particle.Play(false);
        }
        wizardProjectileVisual?.PlayForLaunch(visualMode);
        return true;
    }

    private void RestoreOriginalState()
    {
        transform.localScale = originalRootScale;
        for (int i = 0; i < originalImmediateChildren.Length; i++)
        {
            Transform child = originalImmediateChildren[i];
            if (child != null)
                child.localScale = originalImmediateChildScales[i];
        }

        for (int i = 0; i < sourceParticleSnapshots.Length; i++)
        {
            ParticleSnapshot snapshot = sourceParticleSnapshots[i];
            if (snapshot.Particle == null)
                continue;

            snapshot.Particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            snapshot.Restore(true);
        }

        for (int i = 0; i < colliderSnapshots.Length; i++)
        {
            Collider collider = colliderSnapshots[i].Collider;
            if (collider != null)
                collider.enabled = colliderSnapshots[i].Enabled;
        }

        for (int i = 0; i < rendererSnapshots.Length; i++)
        {
            Renderer renderer = rendererSnapshots[i].Renderer;
            if (renderer != null)
                renderer.enabled = rendererSnapshots[i].Enabled;
        }

        if (wizardProjectileVisual != null)
            wizardProjectileVisual.RestoreForLease();

        if (body != null)
        {
            ClearBodyVelocity();
            body.useGravity = originalUseGravity;
            body.isKinematic = originalIsKinematic;
            body.detectCollisions = originalDetectCollisions;
            body.collisionDetectionMode = originalCollisionDetectionMode;
        }
    }

    private void ClearBodyVelocity()
    {
        if (body == null)
            return;

        bool wasKinematic = body.isKinematic;
        if (wasKinematic)
            body.isKinematic = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.Sleep();
        if (wasKinematic)
            body.isKinematic = true;
    }

    private void FixedUpdate()
    {
        if (phase != LeasePhase.Flying || !HasCurrentAuthority())
            return;

        float remaining = maxDistance - Vector3.Distance(body.position, spawnPosition);
        if (remaining <= 0.001f)
        {
            Finish(null, Vector3.zero);
            return;
        }

        TrackTargetHeight(remaining);
        if (phase != LeasePhase.Flying || !HasCurrentAuthority())
            return;

        float step = Mathf.Min(moveSpeed * Time.fixedDeltaTime, remaining);
        body.MovePosition(body.position + moveDirection * step);
    }

    private void Update()
    {
        if (phase != LeasePhase.Finishing)
            return;

        int leaseId = finishLeaseId;
        int capturedOwnerGeneration = finishOwnerGeneration;
        if (leaseId != LeaseId || capturedOwnerGeneration != ownerGeneration || !HasCurrentAuthority())
            return;

        if (hitSparkDeadline > 0f && Time.time >= hitSparkDeadline)
        {
            wizardProjectileVisual.StopHitSpark();
            hitSparkDeadline = 0f;
        }
        if (Time.time >= finishDeadline)
            RequestReturn(leaseId, capturedOwnerGeneration);
    }

    // 비행 중 높이만 보정: 수평 진행 방향은 플레이어 조준 그대로 유지
    private void TrackTargetHeight(float remaining)
    {
        if (verticalTrackingSpeed <= 0f || planarDirection.sqrMagnitude < 0.001f || !hadAimTarget)
            return;

        if (aimTarget == null || !aimTarget.enabled || aimTargetHealth == null || aimTargetHealth.IsDead)
        {
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

    private void OnTriggerEnter(Collider other)
    {
        if (phase != LeasePhase.Flying || other == null || !HasCurrentAuthority() ||
            damageDealer == null || other.transform.root == damageDealer.transform)
            return;

        EnemyHealth enemy = other.GetComponentInParent<EnemyHealth>();
        if (enemy == null || enemy.IsDead || !directHits.Add(enemy))
            return;

        if (explosionRadius <= 0f && remainingPierces > 0)
        {
            ResolvePiercingHit(enemy, other.ClosestPoint(transform.position));
            return;
        }

        Finish(enemy, other.ClosestPoint(transform.position));
    }

    private void ResolvePiercingHit(EnemyHealth enemy, Vector3 hitPosition)
    {
        int leaseId = LeaseId;
        int capturedOwnerGeneration = ownerGeneration;
        PlayerDamageDealer dealer = damageDealer;
        Vector3 sourcePosition = spawnPosition;
        int hitDamage = damage;
        PlayerDamageType hitType = damageType;

        remainingPierces--;
        phase = LeasePhase.Resolving;
        SetCollidersEnabled(false);
        resolutionDepth++;
        try
        {
            dealer.DealDamage(enemy, hitDamage, hitType, sourcePosition);
            if (!IsLeaseStillValid(leaseId, capturedOwnerGeneration))
                return;

            if (visualMode != ProjectileVisualMode.None)
                wizardProjectileVisual?.OnSuccessfulHit(hitPosition, false);
        }
        catch
        {
            CancelFromOwner();
            throw;
        }
        finally
        {
            resolutionDepth--;
            CompleteResolution();
        }
    }

    private void Finish(EnemyHealth directTarget, Vector3 hitPosition)
    {
        if (phase != LeasePhase.Flying || !HasCurrentAuthority())
            return;

        int leaseId = LeaseId;
        int capturedOwnerGeneration = ownerGeneration;
        phase = LeasePhase.Resolving;
        SetCollidersEnabled(false);
        resolutionDepth++;
        try
        {
            if (explosionRadius > 0f)
                FinishExplosion(directTarget, hitPosition, leaseId, capturedOwnerGeneration);
            else
                FinishDirectHit(directTarget, hitPosition, leaseId, capturedOwnerGeneration);
        }
        catch
        {
            CancelFromOwner();
            throw;
        }
        finally
        {
            resolutionDepth--;
            CompleteResolution();
        }
    }

    private void FinishDirectHit(EnemyHealth directTarget, Vector3 hitPosition, int leaseId,
        int capturedOwnerGeneration)
    {
        if (directTarget == null || damageDealer == null)
        {
            RequestReturn(leaseId, capturedOwnerGeneration);
            return;
        }

        PlayerDamageDealer dealer = damageDealer;
        dealer.DealDamage(directTarget, damage, damageType, spawnPosition);
        if (!IsLeaseStillValid(leaseId, capturedOwnerGeneration))
            return;

        if (visualMode != ProjectileVisualMode.None && wizardProjectileVisual != null)
        {
            wizardProjectileVisual.OnSuccessfulHit(hitPosition, true);
            if (!IsLeaseStillValid(leaseId, capturedOwnerGeneration))
                return;

            StopSourceFlightEffects();
            wizardProjectileVisual.StopFlightKeepingHitSpark();
            ScheduleFinish(HitSparkCleanupDelay, leaseId, capturedOwnerGeneration);
            return;
        }

        RequestReturn(leaseId, capturedOwnerGeneration);
    }

    private void FinishExplosion(EnemyHealth directTarget, Vector3 hitPosition, int leaseId,
        int capturedOwnerGeneration)
    {
        PlayerDamageDealer dealer = damageDealer;
        if (dealer == null)
        {
            RequestReturn(leaseId, capturedOwnerGeneration);
            return;
        }

        IReadOnlyList<EnemyHealth> targets = explosionQuery.Collect(body.position, explosionRadius);
        bool directTargetIncluded = false;
        for (int i = 0; i < targets.Count; i++)
        {
            EnemyHealth enemy = targets[i];
            if (enemy == directTarget)
                directTargetIncluded = true;
            if (enemy == null || enemy.IsDead)
                continue;

            dealer.DealDamage(enemy, damage, damageType, spawnPosition);
            if (!IsLeaseStillValid(leaseId, capturedOwnerGeneration))
                return;
        }

        if (directTarget != null && !directTarget.IsDead && !directTargetIncluded)
        {
            dealer.DealDamage(directTarget, damage, damageType, spawnPosition);
            if (!IsLeaseStillValid(leaseId, capturedOwnerGeneration))
                return;
        }

        if (directTarget != null && visualMode != ProjectileVisualMode.None)
        {
            wizardProjectileVisual?.OnSuccessfulHit(hitPosition, true);
            if (!IsLeaseStillValid(leaseId, capturedOwnerGeneration))
                return;
            hitSparkDeadline = Time.time + HitSparkCleanupDelay;
        }

        EmitExplosionBurst();
        ScheduleFinish(ExplosionCleanupDelay, leaseId, capturedOwnerGeneration);
    }

    private void EmitExplosionBurst()
    {
        // Original Initialize discovered particles after wizard construction, including core and spark.
        ParticleSystem[] particles = visualMode == ProjectileVisualMode.None ? sourceParticles : wizardExplosionParticles;
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            if (particle == null)
                continue;

            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particle.main;
            main.loop = false;
            main.startLifetime = 0.4f;
            main.startSpeed = 3f;
            ParticleSystem.EmissionModule emission = particle.emission;
            emission.enabled = false;
            particle.Play(false);
            particle.Emit(24);
        }
    }

    private void StopSourceFlightEffects()
    {
        for (int i = 0; i < sourceParticles.Length; i++)
        {
            ParticleSystem particle = sourceParticles[i];
            if (particle != null)
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        for (int i = 0; i < rendererSnapshots.Length; i++)
        {
            Renderer renderer = rendererSnapshots[i].Renderer;
            if (renderer != null)
                renderer.enabled = false;
        }
    }

    private void ScheduleFinish(float delay, int leaseId, int capturedOwnerGeneration)
    {
        phase = LeasePhase.Finishing;
        finishLeaseId = leaseId;
        finishOwnerGeneration = capturedOwnerGeneration;
        finishDeadline = Time.time + delay;
    }

    private void RequestReturn(int leaseId, int capturedOwnerGeneration)
    {
        if (leaseId != LeaseId || capturedOwnerGeneration != ownerGeneration ||
            phase == LeasePhase.ReturnPending || phase == LeasePhase.Cancelled || phase == LeasePhase.Disposed ||
            phase == LeasePhase.Idle)
            return;

        phase = LeasePhase.ReturnPending;
        finishDeadline = 0f;
        finishLeaseId = 0;
        finishOwnerGeneration = 0;
        if (resolutionDepth == 0)
            CompleteReturn(leaseId, capturedOwnerGeneration);
    }

    private void CompleteResolution()
    {
        if (resolutionDepth != 0)
            return;

        if (phase == LeasePhase.Cancelled)
        {
            FinalizeCancelledLease();
            return;
        }

        if (phase == LeasePhase.ReturnPending)
        {
            CompleteReturn(LeaseId, ownerGeneration);
            return;
        }

        if (phase == LeasePhase.Resolving)
        {
            if (HasCurrentAuthority())
            {
                phase = LeasePhase.Flying;
                SetColliderSnapshotState();
            }
            else if (phase != LeasePhase.Cancelled)
            {
                CancelFromOwner();
            }
        }
    }

    private void CompleteReturn(int leaseId, int capturedOwnerGeneration)
    {
        if (resolutionDepth != 0 || phase != LeasePhase.ReturnPending || leaseId != LeaseId ||
            capturedOwnerGeneration != ownerGeneration)
            return;

        PlayerProjectilePool owner = poolOwner;
        if (owner != null && owner.ReturnLease(this, leaseId, capturedOwnerGeneration))
            return;

        if (phase == LeasePhase.ReturnPending)
            CancelFromOwner();
    }

    private bool HasCurrentAuthority()
    {
        PlayerProjectilePool owner = poolOwner;
        return owner != null && owner.IsLeaseCurrent(this, LeaseId, ownerGeneration);
    }

    private bool IsLeaseStillValid(int leaseId, int capturedOwnerGeneration)
    {
        if (phase != LeasePhase.Resolving || leaseId != LeaseId || capturedOwnerGeneration != ownerGeneration)
            return false;

        PlayerProjectilePool owner = poolOwner;
        return owner != null && owner.IsLeaseCurrent(this, leaseId, capturedOwnerGeneration);
    }

    private void SetCollidersEnabled(bool enabled)
    {
        for (int i = 0; i < colliderSnapshots.Length; i++)
        {
            Collider collider = colliderSnapshots[i].Collider;
            if (collider != null)
                collider.enabled = enabled && colliderSnapshots[i].Enabled;
        }
    }

    private void SetColliderSnapshotState()
    {
        for (int i = 0; i < colliderSnapshots.Length; i++)
        {
            Collider collider = colliderSnapshots[i].Collider;
            if (collider != null)
                collider.enabled = colliderSnapshots[i].Enabled;
        }
    }

    internal bool IsLeaseCurrent(int leaseId, int capturedOwnerGeneration)
    {
        return leaseId == LeaseId && ownerGeneration == capturedOwnerGeneration &&
               phase != LeasePhase.Idle && phase != LeasePhase.Cancelled && phase != LeasePhase.Disposed;
    }

    internal void CancelFromOwner()
    {
        if (phase == LeasePhase.Idle || phase == LeasePhase.Disposed || phase == LeasePhase.Cancelled)
            return;

        phase = LeasePhase.Cancelled;
        finishDeadline = 0f;
        finishLeaseId = 0;
        finishOwnerGeneration = 0;
        SetCollidersEnabled(false);
        // The owner may destroy its container before a synchronous damage callback unwinds.
        if (resolutionDepth > 0)
            transform.SetParent(null, true);
        if (resolutionDepth == 0)
            FinalizeCancelledLease();
    }

    internal void DisposeFromOwner()
    {
        if (resolutionDepth > 0)
        {
            phase = LeasePhase.Cancelled;
            SetCollidersEnabled(false);
            return;
        }

        phase = LeasePhase.Cancelled;
        FinalizeCancelledLease();
    }

    private void FinalizeCancelledLease()
    {
        if (phase == LeasePhase.Disposed || resolutionDepth != 0)
            return;

        PlayerProjectilePool owner = poolOwner;
        StopAndClearSourceParticles();
        wizardProjectileVisual?.ResetForPool();
        explosionQuery.ClearResults();
        directHits.Clear();
        ClearLeaseReferences();
        phase = LeasePhase.Disposed;
        gameObject.SetActive(false);
        if (owner != null)
            owner.ForgetDisposed(this);
        DestroyForTeardown(gameObject);
    }

    internal void ResetForPool()
    {
        if (resolutionDepth != 0)
            throw new InvalidOperationException("A projectile cannot return while damage callbacks are on the stack.");

        StopAndClearSourceParticles();
        wizardProjectileVisual?.ResetForPool();
        RestoreOriginalState();
        explosionQuery.ClearResults();
        directHits.Clear();
        ClearLeaseReferences();
        phase = LeasePhase.Idle;
        resolutionDepth = 0;
    }

    private void StopAndClearSourceParticles()
    {
        for (int i = 0; i < sourceParticles.Length; i++)
        {
            ParticleSystem particle = sourceParticles[i];
            if (particle != null)
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void ClearLeaseReferences()
    {
        poolOwner = null;
        damageDealer = null;
        damageType = default;
        spawnPosition = Vector3.zero;
        maxDistance = 0f;
        moveSpeed = 0f;
        damage = 0;
        moveDirection = Vector3.zero;
        planarDirection = Vector3.zero;
        aimTarget = null;
        aimTargetHealth = null;
        hadAimTarget = false;
        explosionRadius = 0f;
        remainingPierces = 0;
        visualMode = ProjectileVisualMode.None;
        hitSparkDeadline = 0f;
        finishDeadline = 0f;
        finishLeaseId = 0;
        finishOwnerGeneration = 0;
    }


    internal void MarkDisposedByPool()
    {
        if (phase == LeasePhase.Disposed)
            return;

        phase = LeasePhase.Disposed;
        SetCollidersEnabled(false);
        StopAndClearSourceParticles();
        wizardProjectileVisual?.ResetForPool();
        gameObject.SetActive(false);
    }

    private void OnApplicationQuit()
    {
        if (phase != LeasePhase.Idle)
            CancelFromOwner();
    }

    private static void DestroyForTeardown(GameObject target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private const float HitSparkCleanupDelay = 0.35f;
    private const float ExplosionCleanupDelay = 0.6f;
}
