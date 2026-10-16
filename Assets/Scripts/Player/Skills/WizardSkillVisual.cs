using UnityEngine;

public enum WizardSkillVisualRole
{
    GravityCore,
    GravityVortex,
    GravityImpact,
    ResonanceField,
    ResonanceImpact,
    OverdriveAura,
    OverdriveWave,
    OverdriveFinal
}

// Owns instantiated pack particles; never edits the pack's shared materials.
public sealed class WizardSkillVisual : MonoBehaviour
{
    private struct VisualProfile
    {
        public Color StartColor;
        public Color FirstColor;
        public Color LastColor;
        public float Opacity;
        public float EmissionMultiplier;
        public float LifetimeMultiplier;
        public float SizeMultiplier;
    }

    private ParticleSystem[] systems;
    private Material groundBoundaryMaterial;

    public void Initialize(WizardSkillVisualRole role, bool continuous, bool followsOwner)
    {
        VisualProfile profile = GetProfile(role);
        systems = GetComponentsInChildren<ParticleSystem>(true);
        MaterialPropertyBlock tint = new MaterialPropertyBlock();
        foreach (ParticleSystemRenderer renderer in GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            Material material = renderer.sharedMaterial;
            float intensity = material != null && material.HasProperty("_BaseColor")
                ? Mathf.Max(1f, material.GetColor("_BaseColor").maxColorComponent)
                : 1f;
            bool vortexSmoke = role == WizardSkillVisualRole.GravityVortex &&
                               renderer.transform == transform;
            float opacity = profile.Opacity * (vortexSmoke ? 0.35f : 1f);
            Color roleTint = new Color(intensity, intensity, intensity, opacity);
            tint.SetColor("_BaseColor", roleTint);
            tint.SetColor("_Color", roleTint);
            renderer.SetPropertyBlock(tint);
        }
        foreach (ParticleSystem system in systems)
        {
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = continuous;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;
            main.useUnscaledTime = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startColor = profile.StartColor;
            bool vortexSmoke = role == WizardSkillVisualRole.GravityVortex &&
                               system.transform == transform;
            bool vortexWisp = role == WizardSkillVisualRole.GravityVortex && !vortexSmoke;
            main.startLifetimeMultiplier *= profile.LifetimeMultiplier * (vortexSmoke ? 0.65f : 1f);
            main.startSizeMultiplier *= profile.SizeMultiplier;
            if (followsOwner)
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = system.emission;
            // The vortex prefab root is its broad smoke emitter; preserve its wispy child layer.
            emission.rateOverTimeMultiplier *= vortexWisp ? 0.8f :
                profile.EmissionMultiplier * (vortexSmoke ? 0.12f : 1f);
            if (vortexWisp)
            {
                var velocity = system.velocityOverLifetime;
                velocity.enabled = true;
                velocity.radial = new ParticleSystem.MinMaxCurve(-2f);
            }
            Gradient gradient = RoleGradient(profile);
            var color = system.colorOverLifetime;
            if (color.enabled)
                color.color = gradient;
            var speedColor = system.colorBySpeed;
            if (speedColor.enabled)
                speedColor.color = gradient;
            var trails = system.trails;
            if (trails.enabled)
            {
                trails.colorOverLifetime = gradient;
                trails.colorOverTrail = gradient;
            }
            system.Play(false);
        }
    }

    internal void OwnGroundBoundaryMaterial(Material material)
    {
        groundBoundaryMaterial = material;
    }

    public bool IsAlive()
    {
        if (systems == null) return false;
        foreach (ParticleSystem system in systems)
            if (system != null && system.IsAlive(false)) return true;
        return false;
    }

    private void OnDestroy()
    {
        if (groundBoundaryMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(groundBoundaryMaterial);
            else
                DestroyImmediate(groundBoundaryMaterial);
        }
    }

    private static VisualProfile GetProfile(WizardSkillVisualRole role)
    {
        switch (role)
        {
            case WizardSkillVisualRole.GravityCore:
                return new VisualProfile
                {
                    StartColor = new Color(0.78f, 0.88f, 1f),
                    FirstColor = new Color(0.35f, 0.8f, 1f),
                    LastColor = new Color(0.68f, 0.42f, 1f),
                    Opacity = 0.9f,
                    EmissionMultiplier = 0.62f,
                    LifetimeMultiplier = 0.8f,
                    SizeMultiplier = 1f
                };
            case WizardSkillVisualRole.GravityVortex:
                return new VisualProfile
                {
                    StartColor = new Color(0.76f, 0.84f, 1f),
                    FirstColor = new Color(0.38f, 0.72f, 1f),
                    LastColor = new Color(0.66f, 0.4f, 1f),
                    Opacity = 0.62f,
                    EmissionMultiplier = 0.3f,
                    LifetimeMultiplier = 0.8f,
                    SizeMultiplier = 1f
                };
            case WizardSkillVisualRole.GravityImpact:
                return new VisualProfile
                {
                    StartColor = new Color(0.88f, 0.93f, 1f),
                    FirstColor = new Color(0.58f, 0.46f, 1f),
                    LastColor = new Color(0.34f, 0.72f, 1f),
                    Opacity = 0.95f,
                    EmissionMultiplier = 0.9f,
                    LifetimeMultiplier = 0.9f,
                    SizeMultiplier = 1.15f
                };
            case WizardSkillVisualRole.ResonanceField:
                return new VisualProfile
                {
                    StartColor = new Color(0.82f, 0.78f, 1f),
                    FirstColor = new Color(0.66f, 0.4f, 1f),
                    LastColor = new Color(0.4f, 0.8f, 1f),
                    Opacity = 0.66f,
                    EmissionMultiplier = 0.38f,
                    LifetimeMultiplier = 0.85f,
                    SizeMultiplier = 1f
                };
            case WizardSkillVisualRole.ResonanceImpact:
                return new VisualProfile
                {
                    StartColor = new Color(0.88f, 0.95f, 1f),
                    FirstColor = new Color(0.24f, 0.9f, 1f),
                    LastColor = new Color(0.78f, 0.32f, 0.98f),
                    Opacity = 0.9f,
                    EmissionMultiplier = 1.1f,
                    LifetimeMultiplier = 0.52f,
                    SizeMultiplier = 0.64f
                };
            case WizardSkillVisualRole.OverdriveAura:
                return new VisualProfile
                {
                    StartColor = new Color(0.8f, 0.72f, 1f),
                    FirstColor = new Color(0.67f, 0.42f, 1f),
                    LastColor = new Color(0.42f, 0.7f, 1f),
                    Opacity = 0.48f,
                    EmissionMultiplier = 0.28f,
                    LifetimeMultiplier = 0.82f,
                    SizeMultiplier = 1f
                };
            case WizardSkillVisualRole.OverdriveWave:
                return new VisualProfile
                {
                    StartColor = new Color(0.78f, 0.9f, 1f),
                    FirstColor = new Color(0.38f, 0.82f, 1f),
                    LastColor = new Color(0.63f, 0.48f, 1f),
                    Opacity = 0.68f,
                    EmissionMultiplier = 0.52f,
                    LifetimeMultiplier = 0.82f,
                    SizeMultiplier = 1f
                };
            case WizardSkillVisualRole.OverdriveFinal:
                return new VisualProfile
                {
                    StartColor = new Color(0.92f, 0.86f, 1f),
                    FirstColor = new Color(0.72f, 0.43f, 1f),
                    LastColor = new Color(0.42f, 0.82f, 1f),
                    Opacity = 1f,
                    EmissionMultiplier = 1.2f,
                    LifetimeMultiplier = 0.95f,
                    SizeMultiplier = 1f
                };
            default:
                throw new System.ArgumentOutOfRangeException(nameof(role), role, null);
        }
    }

    private static Gradient RoleGradient(VisualProfile profile)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] {
            new GradientColorKey(profile.FirstColor, 0f),
            new GradientColorKey(profile.LastColor, 1f)
        }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }
}
