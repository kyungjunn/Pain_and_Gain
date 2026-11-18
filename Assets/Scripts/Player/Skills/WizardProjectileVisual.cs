using System.Collections.Generic;
using UnityEngine;

// 마법사 평타 전용 연출: 공용 투사체와 분리
public sealed class WizardProjectileVisual : MonoBehaviour
{
    private const int SourceParticleLimit = 32;
    private const int CoreParticleLimit = 2;
    private const int HitParticleLimit = 24;
    private const float HitParticleLifetime = 0.2f;

    private static readonly Gradient BasicPurple = CreateGradient(new Color(0.64f, 0.38f, 1f));
    private static readonly Gradient BasicCyan = CreateGradient(new Color(0.28f, 0.88f, 1f));
    private static readonly Gradient EmpoweredPurple = CreateGradient(new Color(0.76f, 0.5f, 1f));
    private static readonly Gradient EmpoweredCyan = CreateGradient(new Color(0.42f, 0.96f, 1f));
    private static readonly Gradient BasicFlame = CreateGradient(new Color(0.64f, 0.42f, 1f), 0.2f);
    private static readonly Gradient EmpoweredFlame = CreateGradient(new Color(0.76f, 0.52f, 1f), 0.3f);
    private static readonly Gradient BasicGlow = CreateGradient(new Color(0.36f, 0.78f, 1f), 0.12f);
    private static readonly Gradient EmpoweredGlow = CreateGradient(new Color(0.46f, 0.88f, 1f), 0.18f);
    private static readonly Gradient BasicDust = CreateGradient(new Color(0.28f, 0.82f, 1f), 0.42f);
    private static readonly Gradient EmpoweredDust = CreateGradient(new Color(0.38f, 0.9f, 1f), 0.52f);

    private struct ParticleConstructionSnapshot
    {
        public ParticleSystem Particle;
        public ParticleSystemScalingMode ScalingMode;
        public bool Loop;
        public bool PlayOnAwake;
        public float Duration;
        public ParticleSystem.MinMaxCurve StartLifetime;
        public ParticleSystem.MinMaxCurve StartSpeed;
        public ParticleSystem.MinMaxCurve StartSize;
        public int MaxParticles;
        public ParticleSystemSimulationSpace SimulationSpace;
        public ParticleSystem.MinMaxGradient StartColor;
        public bool ColorOverLifetimeEnabled;
        public ParticleSystem.MinMaxGradient ColorOverLifetime;
        public bool EmissionEnabled;
        public ParticleSystem.MinMaxCurve RateOverTime;
        public bool ShapeEnabled;

        public static ParticleConstructionSnapshot Capture(ParticleSystem particle)
        {
            ParticleSystem.MainModule main = particle.main;
            ParticleSystem.ColorOverLifetimeModule color = particle.colorOverLifetime;
            ParticleSystem.EmissionModule emission = particle.emission;
            ParticleSystem.ShapeModule shape = particle.shape;
            return new ParticleConstructionSnapshot
            {
                Particle = particle,
                ScalingMode = main.scalingMode,
                Loop = main.loop,
                PlayOnAwake = main.playOnAwake,
                Duration = main.duration,
                StartLifetime = main.startLifetime,
                StartSpeed = main.startSpeed,
                StartSize = main.startSize,
                MaxParticles = main.maxParticles,
                SimulationSpace = main.simulationSpace,
                StartColor = main.startColor,
                ColorOverLifetimeEnabled = color.enabled,
                ColorOverLifetime = color.color,
                EmissionEnabled = emission.enabled,
                RateOverTime = emission.rateOverTime,
                ShapeEnabled = shape.enabled
            };
        }

        public void Restore()
        {
            if (Particle == null)
                return;

            ParticleSystem.MainModule main = Particle.main;
            main.scalingMode = ScalingMode;
            main.loop = Loop;
            main.playOnAwake = false;
            main.duration = Duration;
            main.startLifetime = StartLifetime;
            main.startSpeed = StartSpeed;
            main.startSize = StartSize;
            main.maxParticles = MaxParticles;
            main.simulationSpace = SimulationSpace;
            main.startColor = StartColor;

            ParticleSystem.ColorOverLifetimeModule color = Particle.colorOverLifetime;
            color.enabled = ColorOverLifetimeEnabled;
            color.color = ColorOverLifetime;

            ParticleSystem.EmissionModule emission = Particle.emission;
            emission.enabled = EmissionEnabled;
            emission.rateOverTime = RateOverTime;

            ParticleSystem.ShapeModule shape = Particle.shape;
            shape.enabled = ShapeEnabled;
        }
    }

    private struct TrailConstructionSnapshot
    {
        public TrailRenderer Trail;
        public float Time;
        public float StartWidth;
        public float EndWidth;
        public Color StartColor;
        public Color EndColor;
        public LineAlignment Alignment;
        public LineTextureMode TextureMode;
        public int CornerVertices;
        public int CapVertices;
        public float MinVertexDistance;
        public Material SharedMaterial;
        public bool Enabled;

        public static TrailConstructionSnapshot Capture(TrailRenderer trail)
        {
            return new TrailConstructionSnapshot
            {
                Trail = trail,
                Time = trail.time,
                StartWidth = trail.startWidth,
                EndWidth = trail.endWidth,
                StartColor = trail.startColor,
                EndColor = trail.endColor,
                Alignment = trail.alignment,
                TextureMode = trail.textureMode,
                CornerVertices = trail.numCornerVertices,
                CapVertices = trail.numCapVertices,
                MinVertexDistance = trail.minVertexDistance,
                SharedMaterial = trail.sharedMaterial,
                Enabled = trail.enabled
            };
        }

        public void Restore()
        {
            if (Trail == null)
                return;

            Trail.time = Time;
            Trail.startWidth = StartWidth;
            Trail.endWidth = EndWidth;
            Trail.startColor = StartColor;
            Trail.endColor = EndColor;
            Trail.alignment = Alignment;
            Trail.textureMode = TextureMode;
            Trail.numCornerVertices = CornerVertices;
            Trail.numCapVertices = CapVertices;
            Trail.minVertexDistance = MinVertexDistance;
            Trail.sharedMaterial = SharedMaterial;
            Trail.enabled = Enabled;
            Trail.emitting = false;
            Trail.Clear();
        }
    }

    private ParticleSystem[] sourceParticles;
    private float[] sourceStartSizeMultipliers;
    private float[] sourceStartLifetimeMultipliers;
    private int[] sourceMaxParticles;
    private float[] sourceEmissionRateMultipliers;
    private bool sourceCacheBuilt;

    private GameObject coreObject;
    private GameObject trailObject;
    private GameObject hitSparkObject;
    private ParticleSystem core;
    private ParticleSystem hitSpark;
    private ParticleSystemRenderer coreRenderer;
    private ParticleSystemRenderer hitSparkRenderer;
    private TrailRenderer trail;
    private Material glowMaterial;
    private Transform[] generatedImmediateChildren;
    private Vector3[] generatedOriginalScales;
    private ParticleConstructionSnapshot[] generatedParticleSnapshots;
    private TrailConstructionSnapshot generatedTrailSnapshot;
    private bool hasTrailSnapshot;
    private ProjectileVisualMode currentMode;

    internal void CacheSourceParticles(ParticleSystem[] particles)
    {
        if (sourceCacheBuilt)
            return;

        sourceParticles = particles;
        int count = sourceParticles != null ? sourceParticles.Length : 0;
        sourceStartSizeMultipliers = new float[count];
        sourceStartLifetimeMultipliers = new float[count];
        sourceMaxParticles = new int[count];
        sourceEmissionRateMultipliers = new float[count];
        for (int i = 0; i < count; i++)
        {
            ParticleSystem particle = sourceParticles[i];
            ParticleSystem.MainModule main = particle.main;
            ParticleSystem.EmissionModule emission = particle.emission;
            sourceStartSizeMultipliers[i] = main.startSizeMultiplier;
            sourceStartLifetimeMultipliers[i] = main.startLifetimeMultiplier;
            sourceMaxParticles[i] = main.maxParticles;
            sourceEmissionRateMultipliers[i] = emission.rateOverTimeMultiplier;
        }
        sourceCacheBuilt = true;
    }

    // 생성 자식 재사용 · 최초 크기·파티클 복원 · 기본 비활성
    internal void RestoreForLease()
    {
        RestoreGeneratedConstruction();
        SetGeneratedChildrenActive(false);
        currentMode = ProjectileVisualMode.None;
    }

    // None: 생성 연출 숨김 · Basic/Empowered: 현재 발사 설정 적용
    internal void PrepareLease(ProjectileVisualMode mode)
    {
        RestoreForLease();
        currentMode = mode;
        if (mode == ProjectileVisualMode.None)
            return;

        EnsureConstructed();
        RestoreGeneratedConstruction();
        SetGeneratedChildrenActive(true);
        ConfigureSourceParticles(mode == ProjectileVisualMode.Empowered);
        ConfigureGeneratedVisuals(mode == ProjectileVisualMode.Empowered);
    }

    // 원본 크기 × 발사 배율 · 재대여 시 배율 누적 방지
    internal void ApplyLaunchScale(float scale, bool hierarchyParticleScaling, ProjectileVisualMode mode)
    {
        if (mode == ProjectileVisualMode.None || generatedImmediateChildren == null)
            return;

        for (int i = 0; i < generatedImmediateChildren.Length; i++)
        {
            Transform child = generatedImmediateChildren[i];
            if (child != null)
                child.localScale = generatedOriginalScales[i] * scale;
        }

        if (generatedParticleSnapshots == null)
            return;

        for (int i = 0; i < generatedParticleSnapshots.Length; i++)
        {
            ParticleSystem particle = generatedParticleSnapshots[i].Particle;
            if (particle != null)
                particle.scalingMode = hierarchyParticleScaling
                    ? ParticleSystemScalingMode.Hierarchy
                    : generatedParticleSnapshots[i].ScalingMode;
        }
    }

    internal void PlayForLaunch(ProjectileVisualMode mode)
    {
        if (mode == ProjectileVisualMode.None)
            return;

        if (core != null)
        {
            core.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            core.Play(false);
            ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams
            {
                position = Vector3.zero,
                velocity = Vector3.zero,
                startLifetime = 100f,
                startColor = mode == ProjectileVisualMode.Empowered
                    ? EmpoweredPurple.Evaluate(0.5f)
                    : BasicPurple.Evaluate(0.5f),
                startSize = mode == ProjectileVisualMode.Empowered ? 0.25f : 0.21f
            };
            core.Emit(particle, 1);
            particle.startColor = mode == ProjectileVisualMode.Empowered
                ? EmpoweredCyan.Evaluate(0.5f)
                : BasicCyan.Evaluate(0.5f);
            particle.startSize = mode == ProjectileVisualMode.Empowered ? 0.13f : 0.11f;
            core.Emit(particle, 1);
        }

        if (trail != null)
            trail.emitting = true;
    }

    internal void OnSuccessfulHit(Vector3 hitPosition, bool projectileFinishes = false)
    {
        if (currentMode == ProjectileVisualMode.None || hitSpark == null)
            return;

        hitSpark.Play(false);

        Vector3 forward = transform.forward;
        Vector3 right = Vector3.Cross(forward, Vector3.up);
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(forward, Vector3.right);
        right.Normalize();
        Vector3 up = Vector3.Cross(right, forward).normalized;

        bool empowered = currentMode == ProjectileVisualMode.Empowered;
        Color purple = empowered ? new Color(0.78f, 0.52f, 1f) : new Color(0.64f, 0.4f, 1f);
        Color cyan = empowered ? new Color(0.5f, 0.98f, 1f) : new Color(0.32f, 0.9f, 1f);
        EmitSpark(hitPosition, forward * 0.9f, cyan, empowered);
        EmitSpark(hitPosition, -forward * 0.3f + right * 0.7f, purple, empowered);
        EmitSpark(hitPosition, -forward * 0.3f - right * 0.7f, cyan, empowered);
        EmitSpark(hitPosition, up * 0.65f + right * 0.35f, purple, empowered);
        EmitSpark(hitPosition, -up * 0.65f - right * 0.35f, cyan, empowered);
    }

    // 폭발 부모 수명과 분리 · 직접 적중 스파크만 종료
    internal void StopHitSpark()
    {
        if (hitSpark == null)
            return;
        hitSpark.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        hitSpark.gameObject.SetActive(false);
    }

    internal void StopFlightKeepingHitSpark()
    {
        if (core != null)
            core.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
        }
        if (coreObject != null)
            coreObject.SetActive(false);
        if (trailObject != null)
            trailObject.SetActive(false);
    }

    internal void ResetForPool()
    {
        StopAndClearGeneratedParticles();
        RestoreForLease();
    }

    private void EnsureConstructed()
    {
        if (generatedImmediateChildren != null)
            return;

        ParticleSystemRenderer[] sourceRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
        // 원본 재질 선택 유지 · glow 우선 → 첫 재질 → 없으면 코어·트레일 생략
        glowMaterial = FindGlowMaterial(sourceRenderers) ?? FindAnyMaterial(sourceRenderers);
        CreateCore(glowMaterial);
        CreateTrail(glowMaterial);
        CreateHitSpark(glowMaterial);

        var children = new List<Transform>(3);
        if (coreObject != null)
            children.Add(coreObject.transform);
        if (trailObject != null)
            children.Add(trailObject.transform);
        if (hitSparkObject != null)
            children.Add(hitSparkObject.transform);
        generatedImmediateChildren = children.ToArray();
        generatedOriginalScales = new Vector3[generatedImmediateChildren.Length];
        for (int i = 0; i < generatedImmediateChildren.Length; i++)
        {
            generatedOriginalScales[i] = generatedImmediateChildren[i].localScale;
            generatedImmediateChildren[i].gameObject.SetActive(false);
        }

        var particleList = new List<ParticleSystem>(2);
        if (core != null)
            particleList.Add(core);
        if (hitSpark != null)
            particleList.Add(hitSpark);
        generatedParticleSnapshots = new ParticleConstructionSnapshot[particleList.Count];
        for (int i = 0; i < particleList.Count; i++)
            generatedParticleSnapshots[i] = ParticleConstructionSnapshot.Capture(particleList[i]);

        if (trail != null)
        {
            generatedTrailSnapshot = TrailConstructionSnapshot.Capture(trail);
            hasTrailSnapshot = true;
        }
    }

    private void RestoreGeneratedConstruction()
    {
        if (generatedImmediateChildren == null)
            return;

        for (int i = 0; i < generatedImmediateChildren.Length; i++)
        {
            Transform child = generatedImmediateChildren[i];
            if (child != null)
                child.localScale = generatedOriginalScales[i];
        }

        StopAndClearGeneratedParticles();
        if (generatedParticleSnapshots != null)
        {
            for (int i = 0; i < generatedParticleSnapshots.Length; i++)
                generatedParticleSnapshots[i].Restore();
        }

        if (hasTrailSnapshot)
            generatedTrailSnapshot.Restore();
    }

    private void StopAndClearGeneratedParticles()
    {
        if (core != null)
            core.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (hitSpark != null)
            hitSpark.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (trail != null)
        {
            trail.emitting = false;
            trail.Clear();
        }
    }

    private void SetGeneratedChildrenActive(bool active)
    {
        if (generatedImmediateChildren == null)
            return;

        for (int i = 0; i < generatedImmediateChildren.Length; i++)
        {
            Transform child = generatedImmediateChildren[i];
            if (child != null)
                child.gameObject.SetActive(active);
        }
    }

    private void ConfigureSourceParticles(bool empowered)
    {
        if (sourceParticles == null)
            return;

        for (int i = 0; i < sourceParticles.Length; i++)
        {
            ParticleSystem particle = sourceParticles[i];
            if (particle == null)
                continue;

            ParticleSystem.MainModule main = particle.main;
            main.maxParticles = Mathf.Min(sourceMaxParticles[i], SourceParticleLimit);
            main.startColor = Color.white;

            bool sourceFlame = particle.gameObject == gameObject;
            bool cyanGlow = particle.gameObject.name == "glow";
            bool cyanDust = particle.gameObject.name == "dust";
            if (sourceFlame)
            {
                main.startSizeMultiplier = sourceStartSizeMultipliers[i] * (empowered ? 0.38f : 0.3f);
                main.startLifetimeMultiplier = sourceStartLifetimeMultipliers[i] * (empowered ? 0.62f : 0.52f);
            }
            else if (cyanGlow)
            {
                main.startSizeMultiplier = sourceStartSizeMultipliers[i] * (empowered ? 0.22f : 0.18f);
                main.startLifetimeMultiplier = sourceStartLifetimeMultipliers[i] * 0.48f;
            }
            else if (cyanDust)
            {
                main.startSizeMultiplier = sourceStartSizeMultipliers[i] * 0.8f;
                main.startLifetimeMultiplier = sourceStartLifetimeMultipliers[i] * 0.6f;
                ParticleSystem.EmissionModule emission = particle.emission;
                emission.rateOverTimeMultiplier = sourceEmissionRateMultipliers[i] * 0.35f;
            }

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particle.colorOverLifetime;
            Gradient sourceGradient = sourceFlame
                ? (empowered ? EmpoweredFlame : BasicFlame)
                : cyanGlow
                    ? (empowered ? EmpoweredGlow : BasicGlow)
                    : cyanDust
                        ? (empowered ? EmpoweredDust : BasicDust)
                        : GetColorGradient(empowered, false);
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(sourceGradient);
            colorOverLifetime.enabled = true;
        }
    }

    private void ConfigureGeneratedVisuals(bool empowered)
    {
        if (core != null)
        {
            ParticleSystem.MainModule main = core.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 100f;
            main.startLifetime = 100f;
            main.startSpeed = 0f;
            main.startSize = 0.22f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = CoreParticleLimit;
            ParticleSystem.EmissionModule emission = core.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = core.shape;
            shape.enabled = false;
            coreRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        if (trail != null)
        {
            trail.sharedMaterial = glowMaterial;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;
            trail.minVertexDistance = 0.04f;
            trail.time = empowered ? 0.2f : 0.14f;
            trail.startWidth = empowered ? 0.2f : 0.15f;
            trail.endWidth = 0.015f;
            trail.startColor = empowered
                ? new Color(0.7f, 0.95f, 1f, 0.88f)
                : new Color(0.62f, 0.82f, 1f, 0.78f);
            trail.endColor = new Color(0.56f, 0.32f, 1f, 0f);
            trail.enabled = true;
            trail.emitting = false;
        }

        if (hitSpark != null)
        {
            ParticleSystem.MainModule main = hitSpark.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = HitParticleLifetime;
            main.startLifetime = HitParticleLifetime;
            main.startSpeed = 0f;
            main.startSize = empowered ? 0.09f : 0.075f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = HitParticleLimit;
            ParticleSystem.EmissionModule emission = hitSpark.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = hitSpark.shape;
            shape.enabled = false;
            hitSparkRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            hitSparkRenderer.sharedMaterial = glowMaterial;
        }
    }

    private void CreateCore(Material material)
    {
        if (material == null)
            return;

        coreObject = new GameObject("WizardProjectileCore");
        coreObject.transform.SetParent(transform, false);
        core = coreObject.AddComponent<ParticleSystem>();
        core.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = core.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 100f;
        main.startLifetime = 100f;
        main.startSpeed = 0f;
        main.startSize = 0.22f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = CoreParticleLimit;
        ParticleSystem.EmissionModule emission = core.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = core.shape;
        shape.enabled = false;
        coreRenderer = core.GetComponent<ParticleSystemRenderer>();
        coreRenderer.sharedMaterial = material;
        coreRenderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    private void CreateTrail(Material material)
    {
        if (material == null)
            return;

        trailObject = new GameObject("WizardProjectileTrail");
        trailObject.transform.SetParent(transform, false);
        trail = trailObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = material;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.numCornerVertices = 2;
        trail.numCapVertices = 2;
        trail.minVertexDistance = 0.04f;
        trail.time = 0.14f;
        trail.startWidth = 0.15f;
        trail.endWidth = 0.015f;
        trail.startColor = new Color(0.62f, 0.82f, 1f, 0.78f);
        trail.endColor = new Color(0.56f, 0.32f, 1f, 0f);
        trail.emitting = false;
        trail.Clear();
    }

    private void CreateHitSpark(Material material)
    {
        hitSparkObject = new GameObject("WizardProjectileHitSpark");
        hitSparkObject.transform.SetParent(transform, false);
        hitSpark = hitSparkObject.AddComponent<ParticleSystem>();
        hitSpark.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = hitSpark.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = HitParticleLifetime;
        main.startLifetime = HitParticleLifetime;
        main.startSpeed = 0f;
        main.startSize = 0.075f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = HitParticleLimit;
        ParticleSystem.EmissionModule emission = hitSpark.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = hitSpark.shape;
        shape.enabled = false;
        hitSparkRenderer = hitSpark.GetComponent<ParticleSystemRenderer>();
        hitSparkRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        if (material != null)
            hitSparkRenderer.sharedMaterial = material;
    }

    private void EmitSpark(Vector3 position, Vector3 velocity, Color color, bool empowered)
    {
        ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams
        {
            applyShapeToPosition = false,
            position = position,
            velocity = velocity,
            startColor = color,
            startLifetime = HitParticleLifetime,
            startSize = empowered ? 0.09f : 0.075f
        };
        hitSpark.Emit(particle, 1);
    }

    private void OnDisable()
    {
        StopAndClearGeneratedParticles();
    }

    private static Material FindGlowMaterial(ParticleSystemRenderer[] renderers)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].gameObject.name == "glow" && renderers[i].sharedMaterial != null)
                return renderers[i].sharedMaterial;
        }

        return null;
    }

    private static Material FindAnyMaterial(ParticleSystemRenderer[] renderers)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].sharedMaterial != null)
                return renderers[i].sharedMaterial;
        }

        return null;
    }

    private static Gradient GetColorGradient(bool empowered, bool cyanAccent)
    {
        if (empowered)
            return cyanAccent ? EmpoweredCyan : EmpoweredPurple;

        return cyanAccent ? BasicCyan : BasicPurple;
    }

    private static Gradient CreateGradient(Color color)
    {
        return CreateGradient(color, 1f);
    }

    private static Gradient CreateGradient(Color color, float maxAlpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(maxAlpha, 0.15f),
                new GradientAlphaKey(maxAlpha, 0.78f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }
}
