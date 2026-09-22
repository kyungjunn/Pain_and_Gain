using DigitalRuby.PyroParticles;
using UnityEngine;

[CreateAssetMenu(menuName = "Player/Skills/Ninja HwaDun")]
public sealed class NinjaHwaDunSkillSO : PlayerSkillSO
{
    [SerializeField] private GameObject flamethrowerPrefab;
    [SerializeField, Min(0.05f)] private float duration = 2f;
    [SerializeField, Min(0.05f)] private float tickInterval = 0.25f;
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;
    [SerializeField] private Vector3 hitboxSize = new Vector3(1.2f, 1.2f, 6f);
    [SerializeField] private Vector3 hitboxCenter = new Vector3(0f, 0f, 3f);

    public override bool DefersCooldown => true;

    public override bool Cast(PlayerSkillController owner, PlayerDamageType damageType)
    {
        if (flamethrowerPrefab == null || owner == null || owner.SkillOrigin == null ||
            owner.DamageDealer == null)
            return false;

        if (owner.GetComponentInChildren<NinjaHwaDunChannel>(true) != null)
            return false;

        Transform origin = owner.SkillOrigin;
        GameObject instance = Object.Instantiate(flamethrowerPrefab, origin.position, origin.rotation, origin);
        instance.name = "NinjaHwaDunFlamethrower";

        FireBaseScript fire = instance.GetComponent<FireBaseScript>();
        if (fire != null)
            fire.Duration = 99999f;

        BoxCollider box = instance.GetComponent<BoxCollider>();
        if (box == null)
            box = instance.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = hitboxSize;
        box.center = hitboxCenter;

        Rigidbody body = instance.GetComponent<Rigidbody>();
        if (body == null)
            body = instance.AddComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        int damage = Mathf.Max(1, Mathf.RoundToInt(
            (owner.Stats != null ? owner.Stats.AttackDamage : 10f) * damageMultiplier));
        NinjaHwaDunChannel channel = instance.AddComponent<NinjaHwaDunChannel>();
        channel.Initialize(owner, this, damage, tickInterval, duration);
        return true;
    }

    private void OnValidate()
    {
        duration = Mathf.Max(0.05f, duration);
        tickInterval = Mathf.Max(0.05f, tickInterval);
        damageMultiplier = Mathf.Max(0f, damageMultiplier);
        hitboxSize = new Vector3(
            Mathf.Max(0.1f, hitboxSize.x),
            Mathf.Max(0.1f, hitboxSize.y),
            Mathf.Max(0.1f, hitboxSize.z));
    }
}
