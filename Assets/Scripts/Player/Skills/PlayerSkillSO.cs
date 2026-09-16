using UnityEngine;

public abstract class PlayerSkillSO : ScriptableObject
{
    [SerializeField, Min(0f)] private float cooldown = 1f;
    [SerializeField] private string animationTrigger;

    public float Cooldown => cooldown;
    public string AnimationTrigger => animationTrigger;

    public abstract bool Cast(PlayerSkillController owner, PlayerDamageType damageType);
}
