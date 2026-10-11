using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Runtime ownership for the wizard's independently tunable Q/E/R effects.
public sealed class WizardCombat : MonoBehaviour
{
    private sealed class ResonanceField
    {
        public Vector3 Center;
        public float Radius;
        public float EndTime;
        public float NextTickTime;
        public float TickInterval;
        public float ResonanceCooldown;
        public float NextResonanceTime;
        public int TickDamage;
        public int ResonanceDamage;
        public float ResonanceRadius;
        public WizardSkillVisual Visual;
        public readonly SkillAreaQuery TickQuery = new SkillAreaQuery();
        public readonly SkillAreaQuery ResonanceQuery = new SkillAreaQuery();
    }

    private sealed class ResonancePreview
    {
        public WizardSkillSO Skill;
        public bool HasGroundTarget;
        public bool IsValid;
        public Vector3 Center;
        public Vector3 Normal;
        public GameObject VisualRoot;
        public Material LineMaterial;
        public LineRenderer RangeRenderer;
        public LineRenderer FieldRenderer;
    }

    private sealed class Overdrive
    {
        public float EndTime;
        public float NextWaveTime;
        public float WaveInterval;
        public float WaveRadius;
        public float FinalBlastRadius;
        public float FinalDamageMultiplier;
        public float WaveDamageMultiplier;
        public float BasicDamageMultiplier;
        public float BasicVisualScale;
        public int BasicAdditionalPierces;
        public bool Cancelled;
        public bool FinalBlastTriggered;
        public WizardSkillVisual Aura;
        public readonly SkillAreaQuery WaveQuery = new SkillAreaQuery();
        public readonly SkillAreaQuery FinalBlastQuery = new SkillAreaQuery();
    }

    private sealed class PullControl
    {
        public EnemyHealth Enemy;
        public EnemyAI AI;
        public bool AIWasEnabled;
        public bool AgentWasStopped;
        public int References;
    }

    [Header("Visuals")]
    [SerializeField] private GameObject gravityCorePrefab;
    [SerializeField] private GameObject gravityVortexPrefab;
    [SerializeField] private GameObject starHitPrefab;
    [SerializeField] private GameObject resonanceFieldPrefab;
    [SerializeField] private GameObject overdriveAuraPrefab;
    [SerializeField] private GameObject overdriveWavePrefab;
    [SerializeField] private GameObject overdriveFinalPrefab;

    private PlayerSkillController owner;
    private PlayerDamageDealer damageDealer;
    private PlayerStats playerStats;
    private PlayerHealth playerHealth;
    private PlayerStateManager stateManager;
    private ResonanceField resonanceField;
    private ResonancePreview resonancePreview;
    private Overdrive overdrive;
    private WizardSkillSO preparedPreviewCastSkill;
    private Vector3 preparedPreviewCastPosition;
    private Vector3 preparedPreviewCastNormal;
    private int preparedPreviewCastFrame = -1;
    private RaycastHit[] groundHitBuffer = new RaycastHit[16];
    private readonly HashSet<GameObject> activeVisuals = new HashSet<GameObject>();
    private readonly Dictionary<NavMeshAgent, PullControl> controlledAgents =
        new Dictionary<NavMeshAgent, PullControl>();

    public bool IsOverdriveActive =>
        overdrive != null && !overdrive.Cancelled && Time.time < overdrive.EndTime && !IsOwnerDead();

    // Includes synchronous final-blast damage, but not its lingering visual.
    public bool IsUltimateActive =>
        isActiveAndEnabled && overdrive != null && !overdrive.Cancelled && !IsOwnerDead();

    public float BasicDamageMultiplier =>
        IsOverdriveActive ? overdrive.BasicDamageMultiplier : 1f;

    public float BasicVisualScale =>
        IsOverdriveActive ? overdrive.BasicVisualScale : 1f;

    public int BasicAdditionalPierces =>
        IsOverdriveActive ? overdrive.BasicAdditionalPierces : 0;

    public bool IsResonancePreviewActive => resonancePreview != null;
    public bool ResonancePreviewHasGroundTarget =>
        resonancePreview != null && resonancePreview.HasGroundTarget;
    public bool ResonancePreviewIsValid => resonancePreview != null && resonancePreview.IsValid &&
        owner != null && owner.ECharges > 0 && !IsOwnerDead() && !IsGamePaused;
    public Vector3 ResonancePreviewPosition =>
        resonancePreview != null && resonancePreview.HasGroundTarget ? resonancePreview.Center : default;
    public Vector3 ResonancePreviewNormal =>
        resonancePreview != null && resonancePreview.HasGroundTarget ? resonancePreview.Normal : Vector3.up;
    public float ResonancePreviewRange =>
        resonancePreview != null ? resonancePreview.Skill.ResonanceFieldRange : 0f;
    public float ResonancePreviewRadius =>
        resonancePreview != null ? resonancePreview.Skill.ResonanceFieldRadius : 0f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (damageDealer != null)
            damageDealer.OnTargetDamaged += HandleTargetDamaged;
        if (stateManager != null)
            stateManager.OnDead += HandleOwnerDeath;
    }

    private void OnDisable()
    {
        if (damageDealer != null)
            damageDealer.OnTargetDamaged -= HandleTargetDamaged;
        if (stateManager != null)
            stateManager.OnDead -= HandleOwnerDeath;

        CancelAllEffects();
    }

    private void Update()
    {
        if (IsOwnerDead())
        {
            CancelAllEffects();
            return;
        }

        if (IsGamePaused)
        {
            CancelResonancePreview();
            return;
        }

        UpdateResonancePreview();
        if (resonanceField == null)
            return;

        if (Time.time >= resonanceField.EndTime)
        {
            RemoveResonanceField(resonanceField);
            return;
        }

        if (Time.time < resonanceField.NextTickTime)
            return;

        ResonanceField activeField = resonanceField;
        activeField.NextTickTime = Time.time + activeField.TickInterval;
        activeField.TickQuery.DealDamage(
            damageDealer,
            activeField.Center,
            activeField.Radius,
            activeField.TickDamage,
            PlayerDamageType.Skill,
            activeField.Center);
    }

    public bool TryCast(WizardSkillSO skill)
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy || skill == null ||
            owner == null || damageDealer == null || IsOwnerDead() || IsGamePaused)
        {
            return false;
        }

        switch (skill.Kind)
        {
            case WizardSkillKind.GravityCore:
                return TryCastGravityCore(skill);
            case WizardSkillKind.ResonanceField:
                return TryCastResonanceField(skill);
            case WizardSkillKind.Overdrive:
                return TryCastOverdrive(skill);
            default:
                return false;
        }
    }

    public bool TryBeginResonancePreview(WizardSkillSO skill)
    {
        if (IsPointerOverUI() || !isActiveAndEnabled || !gameObject.activeInHierarchy || owner == null ||
            skill == null || skill != owner.SkillE || skill.Kind != WizardSkillKind.ResonanceField ||
            owner.ECharges <= 0 || IsOwnerDead() || IsGamePaused)
        {
            return false;
        }

        if (resonancePreview != null)
            return resonancePreview.Skill == skill;

        resonancePreview = new ResonancePreview
        {
            Skill = skill,
            Normal = Vector3.up
        };
        if (!CreateResonancePreviewVisual(resonancePreview))
        {
            resonancePreview = null;
            return false;
        }

        UpdateResonancePreview();
        return true;
    }

    internal bool TryPrepareResonancePreviewCast(WizardSkillSO skill)
    {
        ClearPreparedResonancePreviewCast();
        if (resonancePreview == null || resonancePreview.Skill != skill ||
            owner == null || owner.ECharges <= 0 || IsOwnerDead() || IsGamePaused)
        {
            return false;
        }

        UpdateResonancePreview();
        if (resonancePreview == null || !resonancePreview.IsValid)
            return false;

        preparedPreviewCastSkill = skill;
        preparedPreviewCastPosition = resonancePreview.Center;
        preparedPreviewCastNormal = resonancePreview.Normal;
        preparedPreviewCastFrame = Time.frameCount;
        return true;
    }

    internal void CancelPreparedResonancePreviewCast()
    {
        ClearPreparedResonancePreviewCast();
    }

    public void CancelResonancePreview()
    {
        if (resonancePreview == null)
            return;

        ResonancePreview preview = resonancePreview;
        resonancePreview = null;
        ClearPreparedResonancePreviewCast();
        if (preview.VisualRoot != null)
        {
            preview.VisualRoot.SetActive(false);
            Destroy(preview.VisualRoot);
        }
        if (preview.LineMaterial != null)
            Destroy(preview.LineMaterial);
        owner?.SuppressBasicAttackForCurrentFrame();
    }

    private bool TryCastGravityCore(WizardSkillSO skill)
    {
        Transform origin = owner.SkillOrigin;
        if (origin == null || origin.forward.sqrMagnitude < 0.001f)
            return false;

        StartCoroutine(RunGravityCore(skill, origin.position, origin.forward.normalized));
        return true;
    }

    private bool TryCastResonanceField(WizardSkillSO skill)
    {
        Vector3 groundPosition;
        Vector3 groundNormal;
        if (preparedPreviewCastSkill == skill && preparedPreviewCastFrame == Time.frameCount)
        {
            groundPosition = preparedPreviewCastPosition;
            groundNormal = preparedPreviewCastNormal;
            ClearPreparedResonancePreviewCast();
        }
        else
        {
            ClearPreparedResonancePreviewCast();
            if (!TryFindGroundPosition(skill.ResonanceFieldRange, out groundPosition, out groundNormal))
                return false;
        }

        if (resonanceField != null)
            RemoveResonanceField(resonanceField);

        ResonanceField field = new ResonanceField
        {
            Center = groundPosition,
            Radius = skill.ResonanceFieldRadius,
            EndTime = Time.time + skill.ResonanceFieldDuration,
            NextTickTime = Time.time,
            TickInterval = skill.ResonanceFieldTickInterval,
            ResonanceCooldown = skill.ResonanceCooldown,
            TickDamage = CalculateDamage(skill.ResonanceFieldTickDamageMultiplier),
            ResonanceDamage = CalculateDamage(skill.ResonanceDamageMultiplier),
            ResonanceRadius = skill.ResonanceRadius
        };
        field.Visual = CreateVisual(resonanceFieldPrefab, "WizardResonanceField", groundPosition + groundNormal * 0.035f, field.Radius / 3f, true);
        if (field.Visual != null)
        {
            field.Visual.transform.rotation = Quaternion.FromToRotation(Vector3.up, groundNormal);
        }
        resonanceField = field;
        CancelResonancePreview();
        return true;
    }

    private bool TryCastOverdrive(WizardSkillSO skill)
    {
        if (overdrive != null)
            return false;

        Overdrive state = new Overdrive
        {
            EndTime = Time.time + skill.OverdriveDuration,
            NextWaveTime = Time.time,
            WaveInterval = skill.OverdriveWaveInterval,
            WaveRadius = skill.OverdriveWaveRadius,
            FinalBlastRadius = skill.OverdriveFinalBlastRadius,
            FinalDamageMultiplier = skill.OverdriveFinalDamageMultiplier,
            WaveDamageMultiplier = skill.OverdriveWaveDamageMultiplier,
            BasicDamageMultiplier = skill.OverdriveBasicDamageMultiplier,
            BasicVisualScale = skill.OverdriveBasicVisualScale,
            BasicAdditionalPierces = skill.OverdriveBasicAdditionalPierces
        };
        state.Aura = CreateVisual(overdriveAuraPrefab, "WizardOverdriveAura", Vector3.up, 1f, true, owner.transform);
        overdrive = state;
        StartCoroutine(RunOverdrive(state));
        return true;
    }

    private IEnumerator RunGravityCore(WizardSkillSO skill, Vector3 position, Vector3 direction)
    {
        WizardSkillVisual coreVisual = CreateVisual(gravityCorePrefab, "WizardGravityCore", position, 0.16f, true);
        if (coreVisual != null)
            coreVisual.transform.rotation = Quaternion.LookRotation(direction);
        WizardSkillVisual vortexVisual = null;

        HashSet<NavMeshAgent> pulledAgents = new HashSet<NavMeshAgent>();
        SkillAreaQuery pullQuery = new SkillAreaQuery();
        RaycastHit[] hitBuffer = new RaycastHit[16];
        Collider[] overlapBuffer = new Collider[16];
        float traveled = 0f;

        try
        {
            bool hasInitialImpact = TryFindCoreOverlap(position, skill.GravityCoreCollisionRadius,
                ref overlapBuffer);
            while (!hasInitialImpact && traveled < skill.GravityCoreRange)
            {
                if (!CanRunEffects())
                    yield break;
                if (IsGamePaused)
                {
                    yield return null;
                    continue;
                }

                float step = Mathf.Min(Time.deltaTime * skill.GravityCoreSpeed,
                    skill.GravityCoreRange - traveled);
                if (step <= 0f)
                {
                    yield return null;
                    continue;
                }

                if (TryFindCoreImpact(position, direction, step, skill.GravityCoreCollisionRadius,
                        ref hitBuffer, out RaycastHit impact))
                {
                    position += direction * impact.distance;
                    if (coreVisual != null)
                        coreVisual.transform.position = position;
                    break;
                }

                position += direction * step;
                traveled += step;
                if (coreVisual != null)
                    coreVisual.transform.position = position;
                yield return null;
            }

            if (TryFindVortexFloor(position, out RaycastHit floor))
            {
                vortexVisual = CreateVisual(gravityVortexPrefab, "WizardGravityVortex",
                    floor.point + floor.normal * 0.04f, skill.GravityCorePullRadius / 4f, true);
                if (vortexVisual != null)
                    vortexVisual.transform.rotation = Quaternion.FromToRotation(Vector3.up, floor.normal);
            }
            float pullEndTime = Time.time + skill.GravityCorePullDuration;
            while (Time.time < pullEndTime)
            {
                if (!CanRunEffects())
                    yield break;
                if (IsGamePaused)
                {
                    yield return null;
                    continue;
                }

                PullNearbyEnemies(position, skill.GravityCorePullRadius, skill.GravityCorePullSpeed,
                    Time.deltaTime, pullQuery, pulledAgents, ref hitBuffer);
                yield return null;
            }

            ReleasePulledAgents(pulledAgents);
            if (!CanRunEffects())
                yield break;

            SkillAreaQuery blastQuery = new SkillAreaQuery();
            blastQuery.DealDamage(
                damageDealer,
                position,
                skill.GravityCoreBlastRadius,
                CalculateDamage(skill.GravityCoreDamageMultiplier),
                PlayerDamageType.Skill,
                owner.transform.position);

            if (CanRunEffects())
            {
                PlayBurst(starHitPrefab, "WizardGravityBlast", position + Vector3.up * 0.04f,
                    skill.GravityCoreBlastRadius / 2f);
            }
        }
        finally
        {
            ReleasePulledAgents(pulledAgents);
            DestroyVisual(coreVisual);
            DestroyVisual(vortexVisual);
        }
    }

    private IEnumerator RunOverdrive(Overdrive state)
    {
        try
        {
            while (Time.time < state.EndTime)
            {
                if (!CanRunEffects() || state.Cancelled)
                    yield break;
                if (IsGamePaused)
                {
                    yield return null;
                    continue;
                }

                if (Time.time >= state.NextWaveTime)
                {
                    state.NextWaveTime = Time.time + state.WaveInterval;
                    Vector3 center = owner.transform.position;
                    state.WaveQuery.DealDamage(
                        damageDealer,
                        center,
                        state.WaveRadius,
                        CalculateDamage(state.WaveDamageMultiplier),
                        PlayerDamageType.Skill,
                        center);
                    if (CanRunEffects() && !state.Cancelled)
                    {
                        PlayBurst(overdriveWavePrefab, "WizardOverdriveWave",
                            center + Vector3.up * 0.04f, state.WaveRadius / 4f);
                    }
                }

                yield return null;
            }

            if (!CanRunEffects() || state.Cancelled || state.FinalBlastTriggered)
                yield break;

            state.FinalBlastTriggered = true;
            DestroyVisual(state.Aura);
            state.Aura = null;

            Vector3 finalCenter = owner.transform.position;
            state.FinalBlastQuery.DealDamage(
                damageDealer,
                finalCenter,
                state.FinalBlastRadius,
                CalculateDamage(state.FinalDamageMultiplier),
                PlayerDamageType.Skill,
                finalCenter);

            if (CanRunEffects())
            {
                PlayBurst(overdriveFinalPrefab, "WizardOverdriveBlast",
                    finalCenter + Vector3.up * 0.04f, state.FinalBlastRadius / 5f);
            }
        }
        finally
        {
            if (overdrive == state)
                overdrive = null;
            DestroyVisual(state.Aura);
            state.Aura = null;
        }
    }

    private bool TryFindVortexFloor(Vector3 position, out RaycastHit floor)
    {
        floor = default;
        float nearest = float.PositiveInfinity;
        foreach (RaycastHit hit in Physics.RaycastAll(position + Vector3.up * 2f, Vector3.down,
            20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (IsOwnerCollider(hit.collider) || hit.collider.GetComponentInParent<EnemyHealth>() != null ||
                hit.distance >= nearest)
                continue;
            nearest = hit.distance;
            floor = hit;
        }
        return nearest < float.PositiveInfinity && Vector3.Dot(floor.normal, Vector3.up) >= 0.5f;
    }

    private void PlayBurst(GameObject prefab, string name, Vector3 position, float scale)
    {
        WizardSkillVisual visual = CreateVisual(prefab, name, position, scale, false);
        if (visual != null)
            StartCoroutine(CleanupBurst(visual));
    }

    private IEnumerator CleanupBurst(WizardSkillVisual visual)
    {
        // Particle lifetime owns the tail, with a bound for malformed assets.
        float endTime = Time.time + 12f;
        yield return null;
        while (visual != null && visual.IsAlive() && Time.time < endTime)
        {
            if (!CanRunEffects())
                break;
            yield return null;
        }
        DestroyVisual(visual);
    }

    private bool TryFindGroundPosition(float range, out Vector3 groundPosition, out Vector3 groundNormal)
    {
        return TryFindGroundCandidate(range, out groundPosition, out groundNormal, out bool withinRange) &&
               withinRange;
    }

    private bool TryFindGroundCandidate(float range, out Vector3 groundPosition, out Vector3 groundNormal,
        out bool withinRange)
    {
        groundPosition = default;
        groundNormal = Vector3.up;
        withinRange = false;
        Camera aimCamera = Camera.main;
        if (aimCamera == null || owner == null)
            return false;

        Ray aimRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int hitCount;
        while (true)
        {
            hitCount = Physics.RaycastNonAlloc(aimRay, groundHitBuffer, aimCamera.farClipPlane, ~0,
                QueryTriggerInteraction.Ignore);
            if (hitCount < groundHitBuffer.Length)
                break;

            System.Array.Resize(ref groundHitBuffer, groundHitBuffer.Length * 2);
        }
        bool hasNearest = false;
        RaycastHit nearest = default;
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = groundHitBuffer[i].collider;
            if (hitCollider == null || IsOwnerCollider(hitCollider) ||
                hitCollider.GetComponentInParent<EnemyHealth>() != null)
            {
                continue;
            }

            if (!hasNearest || groundHitBuffer[i].distance < nearest.distance)
            {
                nearest = groundHitBuffer[i];
                hasNearest = true;
            }
        }

        if (!hasNearest || nearest.normal.y < Mathf.Cos(50f * Mathf.Deg2Rad))
            return false;

        Vector3 flatOffset = nearest.point - owner.transform.position;
        flatOffset.y = 0f;
        groundPosition = nearest.point;
        groundNormal = nearest.normal.normalized;
        withinRange = flatOffset.sqrMagnitude <= range * range;
        return true;
    }

    private bool TryFindCoreOverlap(Vector3 center, float radius, ref Collider[] overlapBuffer)
    {
        int count;
        while (true)
        {
            count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, ~0,
                QueryTriggerInteraction.Collide);
            if (count < overlapBuffer.Length)
                break;

            System.Array.Resize(ref overlapBuffer, overlapBuffer.Length * 2);
        }

        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapBuffer[i];
            overlapBuffer[i] = null;
            if (collider == null || IsOwnerCollider(collider))
                continue;

            EnemyHealth enemy = collider.GetComponentInParent<EnemyHealth>();
            if ((enemy != null && enemy.IsDead) || (enemy == null && collider.isTrigger))
                continue;

            return true;
        }

        return false;
    }

    private bool TryFindCoreImpact(Vector3 origin, Vector3 direction, float distance, float radius,
        ref RaycastHit[] hitBuffer, out RaycastHit nearestHit)
    {
        nearestHit = default;
        float nearestDistance = float.PositiveInfinity;

        int count;
        while (true)
        {
            count = Physics.SphereCastNonAlloc(origin, radius, direction, hitBuffer, distance,
                ~0, QueryTriggerInteraction.Collide);
            if (count < hitBuffer.Length)
                break;

            System.Array.Resize(ref hitBuffer, hitBuffer.Length * 2);
        }

        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hitBuffer[i];
            hitBuffer[i] = default;
            if (hit.collider == null || IsOwnerCollider(hit.collider) || hit.distance >= nearestDistance)
                continue;

            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if ((enemy != null && enemy.IsDead) || (enemy == null && hit.collider.isTrigger))
                continue;

            nearestHit = hit;
            nearestDistance = hit.distance;
            found = true;
        }

        return found;
    }

    private void PullNearbyEnemies(Vector3 center, float radius, float pullSpeed, float deltaTime,
        SkillAreaQuery query, HashSet<NavMeshAgent> pulledAgents, ref RaycastHit[] obstacleBuffer)
    {
        if (pullSpeed <= 0f || deltaTime <= 0f)
            return;

        IReadOnlyList<EnemyHealth> enemies = query.Collect(center, radius);
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth enemy = enemies[i];
            if (enemy == null || enemy.IsDead || enemy.GetComponentInParent<BossEnemyAttack>() != null)
                continue;

            NavMeshAgent agent = enemy.GetComponentInParent<NavMeshAgent>();
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
                continue;

            AcquirePull(agent, enemy, pulledAgents);
            MoveAgentToward(agent, center, pullSpeed, deltaTime, ref obstacleBuffer);
        }
    }

    private void AcquirePull(NavMeshAgent agent, EnemyHealth enemy, HashSet<NavMeshAgent> pulledAgents)
    {
        if (!pulledAgents.Add(agent))
            return;

        if (!controlledAgents.TryGetValue(agent, out PullControl control))
        {
            EnemyAI ai = enemy.GetComponentInParent<EnemyAI>();
            control = new PullControl
            {
                Enemy = enemy,
                AI = ai,
                AIWasEnabled = ai != null && ai.enabled,
                AgentWasStopped = agent.isStopped,
                References = 0
            };
            controlledAgents.Add(agent, control);
            if (control.AIWasEnabled)
                ai.enabled = false;
        }

        control.References++;
        if (agent.enabled && agent.isOnNavMesh)
            agent.isStopped = true;
    }

    private void MoveAgentToward(NavMeshAgent agent, Vector3 center, float pullSpeed, float deltaTime,
        ref RaycastHit[] obstacleBuffer)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || pullSpeed <= 0f || deltaTime <= 0f)
            return;

        Vector3 current = agent.nextPosition;
        Vector3 towardCenter = center - current;
        towardCenter.y = 0f;
        float distance = towardCenter.magnitude;
        float stoppingGap = Mathf.Max(0.3f, agent.radius * 0.65f);
        float remainingDistance = distance - stoppingGap;
        if (remainingDistance <= 0.00001f)
            return;

        Vector3 stoppingPosition = current + towardCenter * (remainingDistance / distance);
        float maxMoveFraction = pullSpeed * deltaTime / remainingDistance;
        float lerpFraction = Mathf.Min(1f - Mathf.Exp(-pullSpeed * deltaTime), maxMoveFraction);
        Vector3 requestedPosition = Vector3.Lerp(current, stoppingPosition, lerpFraction);
        if (!NavMesh.SamplePosition(requestedPosition, out NavMeshHit sampledPosition,
                Mathf.Max(0.2f, agent.radius), agent.areaMask))
        {
            return;
        }

        requestedPosition = sampledPosition.position;
        if (NavMesh.Raycast(current, requestedPosition, out NavMeshHit navMeshHit, agent.areaMask))
            requestedPosition = navMeshHit.position;

        Vector3 horizontalMove = requestedPosition - current;
        horizontalMove.y = 0f;
        float requestedDistance = horizontalMove.magnitude;
        if (requestedDistance <= 0.00001f)
            return;

        Vector3 horizontalDirection = horizontalMove / requestedDistance;
        float allowedDistance = requestedDistance;
        float capsuleRadius = Mathf.Max(0.05f, agent.radius * 0.8f);
        float innerHeight = Mathf.Max(0f, agent.height - capsuleRadius * 2f);
        Vector3 bottom = current + Vector3.up * capsuleRadius;
        Vector3 top = current + Vector3.up * (capsuleRadius + innerHeight);
        int hitCount;
        while (true)
        {
            hitCount = Physics.CapsuleCastNonAlloc(bottom, top, capsuleRadius, horizontalDirection,
                obstacleBuffer, requestedDistance, ~0, QueryTriggerInteraction.Ignore);
            if (hitCount < obstacleBuffer.Length)
                break;

            System.Array.Resize(ref obstacleBuffer, obstacleBuffer.Length * 2);
        }

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = obstacleBuffer[i];
            obstacleBuffer[i] = default;
            if (hit.collider == null || IsOwnerCollider(hit.collider) ||
                hit.collider.GetComponentInParent<EnemyHealth>() != null)
            {
                continue;
            }

            allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - 0.05f));
        }

        if (allowedDistance > 0.00001f)
            agent.Move(horizontalDirection * allowedDistance);
    }

    private void ReleasePulledAgents(HashSet<NavMeshAgent> pulledAgents)
    {
        foreach (NavMeshAgent agent in pulledAgents)
            ReleasePull(agent);
        pulledAgents.Clear();
    }

    private void ReleasePull(NavMeshAgent agent)
    {
        if (object.ReferenceEquals(agent, null) || !controlledAgents.TryGetValue(agent, out PullControl control))
            return;

        control.References--;
        if (control.References > 0)
            return;

        controlledAgents.Remove(agent);
        if (control.Enemy == null || control.Enemy.IsDead)
            return;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = control.AgentWasStopped;
        if (control.AI != null && control.AIWasEnabled)
            control.AI.enabled = true;
    }

    private void RestoreAllPulls()
    {
        foreach (KeyValuePair<NavMeshAgent, PullControl> pair in controlledAgents)
        {
            NavMeshAgent agent = pair.Key;
            PullControl control = pair.Value;
            if (control.Enemy == null || control.Enemy.IsDead)
                continue;

            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = control.AgentWasStopped;
            if (control.AI != null && control.AIWasEnabled)
                control.AI.enabled = true;
        }

        controlledAgents.Clear();
    }

    private void HandleTargetDamaged(EnemyHealth enemy, Vector3 hitPosition, int actualDamage,
        PlayerDamageType damageType)
    {
        ResonanceField field = resonanceField;
        // Finish the hit even when its kill reward opened the paused level-up UI.
        if (!CanRunEffects() || field == null || damageType != PlayerDamageType.BasicAttack || actualDamage <= 0 ||
            Time.time >= field.EndTime || Time.time < field.NextResonanceTime)
        {
            return;
        }

        Vector3 offset = hitPosition - field.Center;
        if (Mathf.Abs(offset.y) > field.Radius)
            return;
        offset.y = 0f;
        if (offset.sqrMagnitude > field.Radius * field.Radius)
            return;

        // Reserve the field's shared cooldown before damage callbacks can re-enter this handler.
        field.NextResonanceTime = Time.time + field.ResonanceCooldown;
        field.ResonanceQuery.DealDamage(
            damageDealer,
            hitPosition,
            field.ResonanceRadius,
            field.ResonanceDamage,
            PlayerDamageType.Skill,
            owner.transform.position);

        if (!CanRunEffects())
            return;

        PlayBurst(starHitPrefab, "WizardResonanceBurst",
            hitPosition + Vector3.up * 0.04f, field.ResonanceRadius / 2f);
    }

    private void RemoveResonanceField(ResonanceField field)
    {
        if (field == null)
            return;

        if (resonanceField == field)
            resonanceField = null;
        DestroyVisual(field.Visual);
        field.Visual = null;
    }

    // Practical authoring radii: circle 3, vortex/slash 4, hit 2, final 5 units.
    // Portal is reduced to a compact core; body aura retains its authored size.
    private WizardSkillVisual CreateVisual(GameObject prefab, string objectName, Vector3 position,
        float scale, bool continuous, Transform parent = null)
    {
        if (prefab == null)
            return null;

        GameObject visualObject = Instantiate(prefab, parent);
        visualObject.name = objectName;
        if (parent != null)
        {
            visualObject.transform.localPosition = position;
        }
        else
        {
            visualObject.transform.position = position;
        }

        visualObject.transform.localScale = Vector3.one * scale;
        WizardSkillVisual visual = visualObject.AddComponent<WizardSkillVisual>();
        activeVisuals.Add(visualObject);
        visual.Initialize(continuous, parent != null);
        return visual;
    }

    private void UpdateResonancePreview()
    {
        ResonancePreview preview = resonancePreview;
        if (preview == null)
            return;

        if (owner == null || preview.Skill == null || preview.Skill != owner.SkillE ||
            preview.Skill.Kind != WizardSkillKind.ResonanceField || owner.ECharges <= 0 || IsOwnerDead())
        {
            CancelResonancePreview();
            return;
        }

        preview.HasGroundTarget = TryFindGroundCandidate(preview.Skill.ResonanceFieldRange,
            out Vector3 position, out Vector3 normal, out bool withinRange);
        if (preview.HasGroundTarget)
        {
            preview.Center = position;
            preview.Normal = normal;
        }

        preview.IsValid = preview.HasGroundTarget && withinRange && owner != null &&
                          owner.ECharges > 0 && !IsOwnerDead() && !IsGamePaused;

        if (preview.RangeRenderer == null)
            return;

        Color rangeColor = preview.IsValid
            ? new Color(0.25f, 0.85f, 1f, 0.95f)
            : new Color(1f, 0.12f, 0.12f, 0.95f);
        Color fieldColor = preview.IsValid
            ? new Color(0.72f, 0.38f, 1f, 0.95f)
            : new Color(1f, 0.12f, 0.12f, 0.95f);
        preview.RangeRenderer.startColor = rangeColor;
        preview.RangeRenderer.endColor = rangeColor;
        SetPreviewRing(preview.RangeRenderer, owner.transform.position, Vector3.up,
            preview.Skill.ResonanceFieldRange, 0.04f, true);

        preview.FieldRenderer.enabled = preview.HasGroundTarget;
        preview.FieldRenderer.startColor = fieldColor;
        preview.FieldRenderer.endColor = fieldColor;
        if (preview.HasGroundTarget)
        {
            SetPreviewRing(preview.FieldRenderer, preview.Center, preview.Normal,
                preview.Skill.ResonanceFieldRadius, 0.055f, false);
        }
    }

    private bool CreateResonancePreviewVisual(ResonancePreview preview)
    {
        Material lineMaterial = null;
        if (resonanceFieldPrefab != null)
        {
            foreach (Renderer renderer in resonanceFieldPrefab.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.sharedMaterial != null)
                {
                    lineMaterial = renderer.sharedMaterial;
                    break;
                }
            }
        }

        if (lineMaterial == null)
            return false;

        preview.LineMaterial = new Material(lineMaterial);
        if (preview.LineMaterial.HasProperty("_BaseMap"))
            preview.LineMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
        if (preview.LineMaterial.HasProperty("_MainTex"))
            preview.LineMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
        if (preview.LineMaterial.HasProperty("_BaseColor"))
            preview.LineMaterial.SetColor("_BaseColor", Color.white);
        if (preview.LineMaterial.HasProperty("_Color"))
            preview.LineMaterial.SetColor("_Color", Color.white);

        preview.VisualRoot = new GameObject("WizardResonancePreview");
        preview.VisualRoot.transform.SetParent(transform, false);
        preview.RangeRenderer = CreatePreviewLine(preview.VisualRoot.transform,
            "WizardResonancePreviewRange", preview.LineMaterial, 0.055f);
        preview.FieldRenderer = CreatePreviewLine(preview.VisualRoot.transform,
            "WizardResonancePreviewField", preview.LineMaterial, 0.075f);
        if (preview.RangeRenderer != null && preview.FieldRenderer != null)
            return true;

        preview.VisualRoot.SetActive(false);
        Destroy(preview.VisualRoot);
        Destroy(preview.LineMaterial);
        preview.VisualRoot = null;
        preview.LineMaterial = null;
        preview.RangeRenderer = null;
        preview.FieldRenderer = null;
        return false;
    }

    private static LineRenderer CreatePreviewLine(Transform parent, string objectName, Material material,
        float width)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(parent, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = 64;
        line.startWidth = width;
        line.endWidth = width;
        line.sharedMaterial = material;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Tile;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private void SetPreviewRing(LineRenderer line, Vector3 center, Vector3 normal,
        float radius, float surfaceOffset, bool projectToGround)
    {
        normal.Normalize();
        Vector3 tangent = Vector3.Cross(normal, Vector3.right);
        if (tangent.sqrMagnitude < 0.001f)
            tangent = Vector3.Cross(normal, Vector3.forward);
        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
        Vector3 ringCenter = center + normal * surfaceOffset;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = Mathf.PI * 2f * i / line.positionCount;
            Vector3 offset = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
            Vector3 point = ringCenter + offset * radius;
            if (projectToGround)
                point = ProjectPreviewPointToGround(point, surfaceOffset);
            line.SetPosition(i, point);
        }
    }

    private Vector3 ProjectPreviewPointToGround(Vector3 point, float surfaceOffset)
    {
        const float ProbeHeight = 2f;
        const float ProbeDistance = 20f;
        Ray ray = new Ray(new Vector3(point.x, Mathf.Max(point.y, owner.transform.position.y) + ProbeHeight,
            point.z), Vector3.down);
        int hitCount;
        while (true)
        {
            hitCount = Physics.RaycastNonAlloc(ray, groundHitBuffer, ProbeDistance, ~0,
                QueryTriggerInteraction.Ignore);
            if (hitCount < groundHitBuffer.Length)
                break;

            System.Array.Resize(ref groundHitBuffer, groundHitBuffer.Length * 2);
        }

        float nearestDistance = float.PositiveInfinity;
        RaycastHit nearest = default;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHitBuffer[i];
            if (hit.collider == null || IsOwnerCollider(hit.collider) ||
                hit.collider.GetComponentInParent<EnemyHealth>() != null ||
                hit.normal.y < Mathf.Cos(50f * Mathf.Deg2Rad) || hit.distance >= nearestDistance)
            {
                continue;
            }

            nearest = hit;
            nearestDistance = hit.distance;
        }

        if (nearestDistance == float.PositiveInfinity)
            return point;

        return new Vector3(point.x, nearest.point.y + surfaceOffset, point.z);
    }

    private void ClearPreparedResonancePreviewCast()
    {
        preparedPreviewCastSkill = null;
        preparedPreviewCastPosition = default;
        preparedPreviewCastNormal = default;
        preparedPreviewCastFrame = -1;
    }

    private void DestroyVisual(WizardSkillVisual visual)
    {
        if (visual == null)
            return;

        GameObject visualObject = visual.gameObject;
        activeVisuals.Remove(visualObject);
        Destroy(visualObject);
    }

    private void CancelAllEffects()
    {
        CancelResonancePreview();
        if (overdrive != null)
            overdrive.Cancelled = true;
        StopAllCoroutines();

        resonanceField = null;
        overdrive = null;
        RestoreAllPulls();

        foreach (GameObject visual in activeVisuals)
        {
            if (visual != null)
                Destroy(visual);
        }
        activeVisuals.Clear();
    }

    private void HandleOwnerDeath()
    {
        CancelAllEffects();
    }

    private bool CanRunEffects()
    {
        return isActiveAndEnabled && owner != null && damageDealer != null && !IsOwnerDead();
    }

    private bool IsOwnerDead()
    {
        return (playerHealth != null && playerHealth.IsDead) ||
               (stateManager != null && stateManager.CurrentState == PlayerState.Dead);
    }

    private bool IsOwnerCollider(Collider other)
    {
        if (owner == null || other == null)
            return false;

        Transform colliderTransform = other.transform;
        return colliderTransform == owner.transform || colliderTransform.IsChildOf(owner.transform);
    }

    private static bool IsPointerOverUI()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        if (eventSystem.IsPointerOverGameObject())
            return true;

        Mouse mouse = Mouse.current;
        return mouse != null && eventSystem.IsPointerOverGameObject(mouse.deviceId);
    }

    private int CalculateDamage(float multiplier)
    {
        float baseDamage = playerStats != null ? playerStats.AttackDamage : 10f;
        return Mathf.Max(0, Mathf.RoundToInt(baseDamage * multiplier));
    }

    private void ResolveReferences()
    {
        owner = GetComponent<PlayerSkillController>();
        damageDealer = GetComponent<PlayerDamageDealer>();
        playerStats = GetComponent<PlayerStats>();
        playerHealth = GetComponent<PlayerHealth>();
        stateManager = GetComponent<PlayerStateManager>();
    }

    private static bool IsGamePaused => Time.timeScale <= 0f ||
        (GameManager.Instance != null && GameManager.Instance.IsPaused);
}
