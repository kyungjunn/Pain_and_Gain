using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerUltimateGauge))]
public sealed class PlayerSkillController : MonoBehaviour
{
    [Header("Skills")]
    [SerializeField] private PlayerSkillSO basicAttack;
    [SerializeField] private PlayerSkillSO skillQ;
    [SerializeField] private PlayerSkillSO skillE;
    [SerializeField] private PlayerSkillSO ultimate;

    [Header("References")]
    [SerializeField] private Transform skillOrigin;
    [SerializeField] private Animator animator;

    private PlayerStats stats;
    private PlayerDamageDealer damageDealer;
    private PlayerUltimateGauge ultimateGauge;
    private PlayerStateManager stateManager;
    private float basicReadyTime;
    private float qReadyTime;
    private float eReadyTime;

    public Transform SkillOrigin => skillOrigin;
    public PlayerStats Stats => stats;
    public PlayerDamageDealer DamageDealer => damageDealer;

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        damageDealer = GetComponent<PlayerDamageDealer>();
        ultimateGauge = GetComponent<PlayerUltimateGauge>();
        stateManager = GetComponent<PlayerStateManager>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    public void OnBasicAttack(InputValue value)
    {
        if (value.isPressed)
            TryCast(basicAttack, ref basicReadyTime, PlayerDamageType.BasicAttack, false);
    }

    public void OnSkillQ(InputValue value)
    {
        if (value.isPressed)
            TryCast(skillQ, ref qReadyTime, PlayerDamageType.Skill, true);
    }

    public void OnSkillE(InputValue value)
    {
        if (value.isPressed)
            TryCast(skillE, ref eReadyTime, PlayerDamageType.Skill, true);
    }

    public void OnUltimate(InputValue value)
    {
        if (!value.isPressed || IsSkillCasting() || ultimate == null || !ultimateGauge.IsFull)
            return;

        if (ultimate.Cast(this, PlayerDamageType.Skill) && ultimateGauge.TryConsume())
            PlayAnimation(ultimate);
    }

    private void TryCast(
        PlayerSkillSO skill,
        ref float readyTime,
        PlayerDamageType damageType,
        bool canInterruptBasicAttack)
    {
        bool animationBlocked = canInterruptBasicAttack ? IsSkillCasting() : IsAttacking();
        if (animationBlocked || skill == null || Time.time < readyTime || !skill.Cast(this, damageType))
            return;

        readyTime = Time.time + skill.Cooldown;
        PlayAnimation(skill);
    }

    private void PlayAnimation(PlayerSkillSO skill)
    {
        stateManager?.ChangeState(PlayerState.Attack);

        if (animator != null && !string.IsNullOrWhiteSpace(skill.AnimationTrigger))
            animator.SetTrigger(skill.AnimationTrigger);
    }

    private bool IsAttacking()
    {
        if (stateManager != null && stateManager.CurrentState == PlayerState.Attack)
            return true;

        if (animator == null)
            return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (IsAttackState(current))
            return true;

        return animator.IsInTransition(0) && IsAttackState(animator.GetNextAnimatorStateInfo(0));
    }

    private bool IsSkillCasting()
    {
        if (animator == null)
            return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (IsSkillState(current))
            return true;

        return animator.IsInTransition(0) && IsSkillState(animator.GetNextAnimatorStateInfo(0));
    }

    private static bool IsAttackState(AnimatorStateInfo state)
    {
        return state.IsName("Attack") || IsSkillState(state);
    }

    private static bool IsSkillState(AnimatorStateInfo state)
    {
        return state.IsName("SkillQ") ||
               state.IsName("SkillE") ||
               state.IsName("UltimateR");
    }
}
