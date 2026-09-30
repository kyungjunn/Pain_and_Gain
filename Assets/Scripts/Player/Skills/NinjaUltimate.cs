using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class NinjaUltimate : MonoBehaviour
{
    private const float MinimumDuration = 0.05f;
    private const float MinimumDistance = 0.1f;
    private const int MaximumTrailInstances = 256;

    [Header("References")]
    [SerializeField] private GameObject lightningPrefab;
    [SerializeField] private Transform handAnchor;
    [SerializeField] private Animator animator;

    [Header("Ready")]
    [SerializeField, Min(MinimumDuration)] private float readyDuration = 5f;
    [SerializeField, Min(0f)] private float attackBonusRatio = 0.5f;
    [SerializeField, Min(0f)] private float moveBonusRatio = 0.3f;

    [Header("Dash and Storm")]
    [SerializeField, Min(MinimumDistance)] private float dashDistance = 7f;
    [SerializeField, Min(MinimumDuration)] private float dashDuration = 0.28f;
    [SerializeField, Min(MinimumDuration)] private float stormDuration = 3f;
    [SerializeField, Min(MinimumDistance)] private float stormRadius = 2f;
    [SerializeField, Min(MinimumDuration)] private float stormTickInterval = 0.5f;
    [SerializeField, Min(0f)] private float stormDamageMultiplier = 3f;
    [SerializeField, Min(MinimumDistance)] private float trailSpacing = 2f;
    [SerializeField] private LayerMask enemyLayers = ~0;

    private PlayerStats stats;
    private PlayerDamageDealer damageDealer;
    private PlayerSkillController skillController;
    private PlayerStateManager stateManager;
    private PlayerController playerController;
    private PlayerHealth playerHealth;

    private readonly List<GameObject> trailEffects = new List<GameObject>();
    private GameObject handEffect;
    private Coroutine dashAndStormRoutine;
    private bool readyActive;
    private bool isDashing;
    private float readyEndTime;
    private bool dashControlActive;
    private bool movementLockBeforeDash;

    public bool IsReady => readyActive && Time.time < readyEndTime && !IsPlayerDead();
    public bool IsDashing => isDashing && !IsPlayerDead();

    private void Awake()
    {
        CacheComponents();
    }

    private void Update()
    {
        if (IsPlayerDead())
        {
            Cancel();
            return;
        }

        if (readyActive && Time.time >= readyEndTime)
            ExpireReadyState();
    }

    private void LateUpdate()
    {
        if (readyActive && handEffect != null && handAnchor != null)
        {
            handEffect.transform.SetPositionAndRotation(handAnchor.position, handAnchor.rotation);
        }
    }

    private void OnDisable()
    {
        Cancel();
    }

    public bool TryActivate()
    {
        CacheComponents();

        if (readyActive && Time.time >= readyEndTime)
            ExpireReadyState();

        if (!CanUseAbility() || readyActive || isDashing || dashAndStormRoutine != null ||
            !HasRequiredReferences())
        {
            return false;
        }

        GameObject effect = SpawnEffect(handAnchor.position, handAnchor.rotation, handAnchor.parent, Vector3.one, true);
        if (effect == null)
            return false;

        float attackBonus = stats.AttackDamage * SafeNonNegative(attackBonusRatio, 0f);
        float moveBonus = stats.MoveSpeed * SafeNonNegative(moveBonusRatio, 0f);

        handEffect = effect;
        readyEndTime = Time.time + SafeAtLeast(readyDuration, MinimumDuration, 5f);
        readyActive = true;
        stats.SetTemporaryBonus(this, AugmentType.AttackDamage, attackBonus);
        stats.SetTemporaryBonus(this, AugmentType.MoveSpeed, moveBonus);
        animator.CrossFadeInFixedTime("UltimateReady", 0.05f, 1);
        return true;
    }

    public bool TryRecast()
    {
        CacheComponents();

        if (readyActive && Time.time >= readyEndTime)
            ExpireReadyState();

        if (!IsReady || !CanUseAbility() || isDashing || dashAndStormRoutine != null ||
            !HasRequiredReferences() || skillController.IsMovementLocked)
        {
            return false;
        }

        float buffedAttackDamage = stats.AttackDamage;
        int stormDamage = CalculateStormDamage(buffedAttackDamage);
        Vector3 direction = transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            direction = Vector3.forward;
        else
            direction.Normalize();

        ExpireReadyState();

        movementLockBeforeDash = skillController.IsMovementLocked;
        dashControlActive = true;
        skillController.SetMovementLocked(true);
        isDashing = true;
        stateManager.ChangeState(PlayerState.Attack);
        animator.CrossFadeInFixedTime("UltimateDash", 0.05f, 1);

        dashAndStormRoutine = StartCoroutine(DashAndStorm(direction, stormDamage));
        return true;
    }

    public void Cancel()
    {
        CacheComponents();

        Coroutine activeRoutine = dashAndStormRoutine;
        dashAndStormRoutine = null;
        if (activeRoutine != null)
            StopCoroutine(activeRoutine);

        readyActive = false;
        readyEndTime = 0f;
        stats?.RemoveTemporaryBonuses(this);
        DestroyEffect(ref handEffect);
        FinishDashControl();
        ClearTrailEffects();
        ClearUltimateAnimationIfActive();
    }

    private IEnumerator DashAndStorm(Vector3 direction, int stormDamage)
    {
        Vector3 dashStart = transform.position;
        float distance = SafeAtLeast(dashDistance, MinimumDistance, 7f);
        float duration = SafeAtLeast(dashDuration, MinimumDuration, 0.28f);
        float elapsed = 0f;
        float nextTrailDistance = 0f;
        int trailCount = 0;
        HashSet<EnemyHealth> hitThisTick = new HashSet<EnemyHealth>();

        try
        {
            while (elapsed < duration)
            {
                if (IsPlayerDead())
                    yield break;

                if (IsPaused())
                {
                    yield return null;
                    continue;
                }

                float step = Mathf.Min(Time.deltaTime, duration - elapsed);
                if (step <= 0f)
                {
                    yield return null;
                    continue;
                }

                elapsed += step;
                float traveledDistance = distance * Mathf.Clamp01(elapsed / duration);
                transform.position = dashStart + direction * traveledDistance;
                SpawnTrailEffects(dashStart, direction, distance, traveledDistance,
                    ref nextTrailDistance, ref trailCount);
                yield return null;
            }

            Vector3 dashEnd = transform.position;
            FinishDashControl();

            float stormEndTime = Time.time + SafeAtLeast(stormDuration, MinimumDuration, 3f);
            float nextTickTime = Time.time;
            float tickInterval = SafeAtLeast(stormTickInterval, MinimumDuration, 0.5f);
            float radius = SafeAtLeast(stormRadius, MinimumDistance, 2f);

            while (Time.time < stormEndTime)
            {
                if (IsPlayerDead())
                    yield break;

                if (IsPaused())
                {
                    yield return null;
                    continue;
                }

                if (Time.time >= nextTickTime)
                {
                    ApplyStormDamage(dashStart, dashEnd, radius, stormDamage, hitThisTick);
                    nextTickTime = Time.time + tickInterval;
                }

                yield return null;
            }
        }
        finally
        {
            FinishDashControl();
            ClearTrailEffects();
            ClearUltimateAnimationIfActive();
            dashAndStormRoutine = null;
        }
    }

    private void SpawnTrailEffects(
        Vector3 dashStart,
        Vector3 direction,
        float distance,
        float traveledDistance,
        ref float nextTrailDistance,
        ref int trailCount)
    {
        float spacing = SafeAtLeast(trailSpacing, MinimumDistance, 2f);
        while (nextTrailDistance <= traveledDistance && nextTrailDistance <= distance &&
               trailCount < MaximumTrailInstances)
        {
            Vector3 position = dashStart + direction * nextTrailDistance;
            GameObject effect = SpawnEffect(position, transform.rotation, null, Vector3.one * 10f, false);
            if (effect != null)
                trailEffects.Add(effect);

            trailCount++;
            nextTrailDistance += spacing;
        }
    }

    private void ApplyStormDamage(
        Vector3 dashStart,
        Vector3 dashEnd,
        float radius,
        int damage,
        HashSet<EnemyHealth> hitThisTick)
    {
        if (damage <= 0 || damageDealer == null)
            return;

        hitThisTick.Clear();
        Collider[] overlaps = Physics.OverlapCapsule(
            dashStart, dashEnd, radius, enemyLayers, QueryTriggerInteraction.Collide);

        for (int i = 0; i < overlaps.Length; i++)
        {
            EnemyHealth enemy = overlaps[i].GetComponentInParent<EnemyHealth>();
            if (enemy != null && !enemy.IsDead && hitThisTick.Add(enemy))
                damageDealer.DealDamage(enemy, damage, PlayerDamageType.Skill);
        }
    }

    private GameObject SpawnEffect(Vector3 position, Quaternion rotation, Transform parent, Vector3 scale, bool enableBloom)
    {
        if (lightningPrefab == null)
            return null;

        GameObject effect = Instantiate(lightningPrefab, position, rotation);
        if (parent != null)
            effect.transform.SetParent(parent, true);

        effect.transform.localScale = scale;
        foreach (var volume in effect.GetComponentsInChildren<UnityEngine.Rendering.Volume>(true))
            volume.enabled = enableBloom;
        ConfigureParticles(effect);
        return effect;
    }

    private static void ConfigureParticles(GameObject effect)
    {
        ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            ParticleSystem.MainModule main = particle.main;
            main.loop = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.useUnscaledTime = false;
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play(true);
        }
    }

    private int CalculateStormDamage(float buffedAttackDamage)
    {
        float multiplier = SafeNonNegative(stormDamageMultiplier, 3f);
        if (multiplier <= 0f)
            return 0;

        return Mathf.Max(1, Mathf.RoundToInt(buffedAttackDamage * multiplier));
    }

    private bool CanUseAbility()
    {
        return isActiveAndEnabled && !IsPaused() && !IsPlayerDead();
    }

    private bool HasRequiredReferences()
    {
        return lightningPrefab != null && handAnchor != null && animator != null && animator.layerCount > 1 &&
               stats != null && damageDealer != null && skillController != null && stateManager != null;
    }

    private void CacheComponents()
    {
        if (stats == null)
            stats = GetComponent<PlayerStats>();
        if (damageDealer == null)
            damageDealer = GetComponent<PlayerDamageDealer>();
        if (skillController == null)
            skillController = GetComponent<PlayerSkillController>();
        if (stateManager == null)
            stateManager = GetComponent<PlayerStateManager>();
        if (playerController == null)
            playerController = GetComponent<PlayerController>();
        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    private bool IsPlayerDead()
    {
        return (stateManager != null && stateManager.CurrentState == PlayerState.Dead) ||
               (playerHealth != null && playerHealth.IsDead);
    }

    private static bool IsPaused()
    {
        return GameManager.Instance != null && GameManager.Instance.IsPaused;
    }

    private void ExpireReadyState()
    {
        readyActive = false;
        readyEndTime = 0f;
        stats?.RemoveTemporaryBonuses(this);
        DestroyEffect(ref handEffect);
        ClearUltimateAnimationIfActive();
    }

    private void FinishDashControl()
    {
        bool restoreState = dashControlActive;
        isDashing = false;
        ClearUltimateAnimationIfActive();
        if (!restoreState)
            return;

        dashControlActive = false;
        if (skillController != null)
            skillController.SetMovementLocked(movementLockBeforeDash);

        if (IsPlayerDead())
            return;

        if (playerController != null)
            playerController.EndAttackState();
        else if (stateManager != null)
            stateManager.ChangeState(PlayerState.Idle);
    }

    private void ClearUltimateAnimationIfActive()
    {
        if (animator == null || animator.layerCount <= 1)
            return;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(1);
        bool transitioning = animator.IsInTransition(1);
        AnimatorStateInfo next = transitioning ? animator.GetNextAnimatorStateInfo(1) : default;

        if (transitioning && !IsUltimateAnimation(next))
            return;

        if (IsUltimateAnimation(current) || (transitioning && IsUltimateAnimation(next)))
            animator.CrossFadeInFixedTime("Empty", 0.05f, 1);
    }

    private static bool IsUltimateAnimation(AnimatorStateInfo state)
    {
        return state.IsName("UltimateReady") || state.IsName("UltimateDash");
    }

    private void ClearTrailEffects()
    {
        for (int i = 0; i < trailEffects.Count; i++)
        {
            if (trailEffects[i] != null)
                Destroy(trailEffects[i]);
        }

        trailEffects.Clear();
    }

    private static void DestroyEffect(ref GameObject effect)
    {
        if (effect != null)
            Destroy(effect);
        effect = null;
    }

    private void OnValidate()
    {
        readyDuration = SafeAtLeast(readyDuration, MinimumDuration, 5f);
        attackBonusRatio = SafeNonNegative(attackBonusRatio, 0.5f);
        moveBonusRatio = SafeNonNegative(moveBonusRatio, 0.3f);
        dashDistance = SafeAtLeast(dashDistance, MinimumDistance, 7f);
        dashDuration = SafeAtLeast(dashDuration, MinimumDuration, 0.28f);
        stormDuration = SafeAtLeast(stormDuration, MinimumDuration, 3f);
        stormRadius = SafeAtLeast(stormRadius, MinimumDistance, 2f);
        stormTickInterval = SafeAtLeast(stormTickInterval, MinimumDuration, 0.5f);
        stormDamageMultiplier = SafeNonNegative(stormDamageMultiplier, 3f);
        trailSpacing = SafeAtLeast(trailSpacing, MinimumDistance, 2f);
    }

    private static float SafeAtLeast(float value, float minimum, float fallback)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return fallback;
        return Mathf.Max(minimum, value);
    }

    private static float SafeNonNegative(float value, float fallback)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return fallback;
        return Mathf.Max(0f, value);
    }
}
