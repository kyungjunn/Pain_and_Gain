using UnityEngine;

// Owns instantiated pack particles; never edits the pack's shared materials.
public sealed class WizardSkillVisual : MonoBehaviour
{
    private ParticleSystem[] systems;

    public void Initialize(bool continuous, bool followsOwner)
    {
        systems = GetComponentsInChildren<ParticleSystem>(true);
        MaterialPropertyBlock tint = new MaterialPropertyBlock();
        foreach (ParticleSystemRenderer renderer in GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            Material material = renderer.sharedMaterial;
            float intensity = material != null && material.HasProperty("_BaseColor")
                ? Mathf.Max(1f, material.GetColor("_BaseColor").maxColorComponent)
                : 1f;
            Color neutralTint = new Color(intensity, intensity, intensity, 1f);
            tint.SetColor("_BaseColor", neutralTint);
            tint.SetColor("_Color", neutralTint);
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
            if (followsOwner)
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
            // Replace authored red gradients too, not just startColor (which multiplies them).
            main.startColor = new Color(0.55f, 0.4f, 1f, 0.85f);
            if (gameObject.name == "WizardGravityVortex")
            {
                var emission = system.emission;
                emission.rateOverTimeMultiplier *= 0.45f;
            }
            var color = system.colorOverLifetime;
            if (color.enabled)
                color.color = PurpleGradient();
            var speedColor = system.colorBySpeed;
            if (speedColor.enabled)
                speedColor.color = PurpleGradient();
            var trails = system.trails;
            if (trails.enabled)
            {
                trails.colorOverLifetime = PurpleGradient();
                trails.colorOverTrail = PurpleGradient();
            }
            system.Play(false);
        }
    }

    public bool IsAlive()
    {
        if (systems == null) return false;
        foreach (ParticleSystem system in systems)
            if (system != null && system.IsAlive(false)) return true;
        return false;
    }

    private static Gradient PurpleGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] {
            new GradientColorKey(new Color(0.35f, 0.65f, 1f), 0f),
            new GradientColorKey(new Color(0.65f, 0.3f, 1f), 1f)
        }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }
}
