// 캐릭터 고유 Q/E/R 시전.
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerUltimateGauge))]
public sealed class PlayerSkillController : MonoBehaviour
{
    [Header("Skills")]
    // Q 스킬
    [SerializeField] private PlayerSkillSO skillQ;
    // E 스킬
    [SerializeField] private PlayerSkillSO skillE;
    // R 궁극기
    [SerializeField] private PlayerSkillSO ultimate;

    [Header("References")]
    // 시전 위치
    [SerializeField] private Transform skillOrigin;
    // 스킬 애니메이터
    [SerializeField] private Animator animator;

    private PlayerStats stats;
    private PlayerDamageDealer damageDealer;
    private PlayerUltimateGauge ultimateGauge;
    private PlayerStateManager stateManager;
    // Q 재사용 가능 시각
    private float qReadyTime;
    // E 재사용 가능 시각
    private float eReadyTime;
    private bool movementLocked;
    private PlayerSkillSO deferredCooldownSkill;

    public Transform SkillOrigin => skillOrigin;
    public PlayerStats Stats => stats;
    public PlayerDamageDealer DamageDealer => damageDealer;
    public bool IsMovementLocked => movementLocked;

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;
    }

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        damageDealer = GetComponent<PlayerDamageDealer>();
        ultimateGauge = GetComponent<PlayerUltimateGauge>();
        stateManager = GetComponent<PlayerStateManager>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (deferredCooldownSkill != null &&
            stateManager != null &&
            stateManager.CurrentState == PlayerState.Dead)
            CompleteDeferredSkill(deferredCooldownSkill);
    }

    private void OnDisable()
    {
        if (deferredCooldownSkill != null)
            CompleteDeferredSkill(deferredCooldownSkill);
    }

    // Q 입력
    public void OnSkillQ(InputValue value)
    {
        if (value.isPressed)
            TryCast(skillQ, ref qReadyTime);
    }

    // E 입력
    public void OnSkillE(InputValue value)
    {
        if (value.isPressed)
            TryCast(skillE, ref eReadyTime);
    }

    // R 입력. 게이지 풀일 때만 시전.
    public void OnUltimate(InputValue value)
    {
        if (!value.isPressed || !CanCast() || IsSkillCasting() || ultimate == null || !ultimateGauge.IsFull)
            return;

        if (ultimate.Cast(this, PlayerDamageType.Skill) && ultimateGauge.TryConsume())
            PlayAnimation(ultimate);
    }

    public void NotifyChannelFinished(PlayerSkillSO skill)
    {
        CompleteDeferredSkill(skill);
    }

    // 쿨다운 확인 후 시전
    private void TryCast(
        PlayerSkillSO skill,
        ref float readyTime)
    {
        if (!CanCast() || IsSkillCasting() || skill == null || Time.time < readyTime ||
            !skill.Cast(this, PlayerDamageType.Skill))
            return;

        PlayAnimation(skill);
        if (skill.DefersCooldown)
            deferredCooldownSkill = skill;
        else
            readyTime = Time.time + skill.Cooldown;
    }

    private void CompleteDeferredSkill(PlayerSkillSO skill)
    {
        if (skill == null || deferredCooldownSkill != skill)
            return;

        deferredCooldownSkill = null;
        if (skill == skillQ)
            qReadyTime = Time.time + skill.Cooldown;
        else if (skill == skillE)
            eReadyTime = Time.time + skill.Cooldown;

        if (animator != null && !string.IsNullOrWhiteSpace(skill.AnimationTrigger))
        {
            animator.ResetTrigger(skill.AnimationTrigger);
            animator.CrossFade("Idle", 0.05f, 0);
        }

        GetComponent<PlayerController>()?.EndAttackState();
    }

    // 스킬 애니메이션
    private void PlayAnimation(PlayerSkillSO skill)
    {
        stateManager?.ChangeState(PlayerState.Attack);

        if (animator != null && !string.IsNullOrWhiteSpace(skill.AnimationTrigger))
            animator.SetTrigger(skill.AnimationTrigger);
    }

    // 일시정지/사망 차단
    private bool CanCast()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return false;

        return stateManager == null || stateManager.CurrentState != PlayerState.Dead;
    }

    // 스킬 모션 중 중복 시전 차단
    private bool IsSkillCasting()
    {
        if (deferredCooldownSkill != null)
            return true;

        if (animator == null)
            return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (IsSkillState(current))
            return true;

        return animator.IsInTransition(0) && IsSkillState(animator.GetNextAnimatorStateInfo(0));
    }

    // 스킬 상태명
    private static bool IsSkillState(AnimatorStateInfo state)
    {
        return state.IsName("SkillQ") ||
               state.IsName("SkillE") ||
               state.IsName("UltimateR");
    }
}
