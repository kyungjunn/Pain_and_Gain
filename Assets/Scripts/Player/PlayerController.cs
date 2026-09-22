using UnityEngine;

public class PlayerController : MonoBehaviour
{
    
    private PlayerInputHandler input;
    private PlayerMovement movement;
    private IPlayerBasicAttack playerAttack;
    private Animator anim;
    private PlayerStateManager stateManager;
    private PlayerSkillController skillController;
    private bool basicAttackActive;

    private void Awake()
    {
        EnsureCombatComponents();
    }

    private void Start()
    {
        input = GetComponent<PlayerInputHandler>();
        movement = GetComponent<PlayerMovement>();
        playerAttack = GetComponent<IPlayerBasicAttack>();
        anim = GetComponentInChildren<Animator>();
        stateManager = GetComponent<PlayerStateManager>();
        skillController = GetComponent<PlayerSkillController>();
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            if (input != null)
            {
                input.AttackTriggered = false;
                input.JumpTriggered = false;
            }

            return;
        }

        if (stateManager.CurrentState == PlayerState.Dead)
        {
            return;
        }

        if (skillController == null || !skillController.IsMovementLocked)
            movement.Move(input.MoveInput);

        bool isGrounded = movement.CheckGrounded();

        UpdateMovementState(isGrounded);

        if (anim != null)
        {
            bool isMoving = input.MoveInput.sqrMagnitude > 0.01f;
            float moveSpeedParameter = !isMoving ? 0f :
                input.MoveInput.y < -0.01f ? -1f : 1f;

            anim.SetFloat("MoveSpeed", moveSpeedParameter);
            anim.SetFloat("LegSpeed", isGrounded ? 1f : 0f);
        }

        if (input.AttackTriggered)
        {
            // 공격 중 입력은 소비만 한다. Trigger를 다시 쌓으면 공격 종료 직후
            // 애니메이션만 재시작되고 발사체와 공격 판정이 어긋날 수 있다.
            if (stateManager.CurrentState != PlayerState.Attack &&
                playerAttack != null && playerAttack.TryAttack())
            {
                stateManager.ChangeState(PlayerState.Attack);
                basicAttackActive = true;

                if (anim != null)
                {
                    anim.SetTrigger("Attack");
                }

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayPlayerAttack();
                }

            }

            input.AttackTriggered = false;
        }

        if (input.JumpTriggered)
        {
            if (movement.Jump() && anim != null)
            {
                anim.SetTrigger("Jump");
            }

            input.JumpTriggered = false;
        }
    }

    private void EnsureCombatComponents()
    {
        if (!TryGetComponent(out PlayerHealth _))
        {
            gameObject.AddComponent<PlayerHealth>();
        }

        if (!TryGetComponent(out PlayerDamageDealer _))
        {
            gameObject.AddComponent<PlayerDamageDealer>();
        }

    }

    private void UpdateMovementState(bool isGrounded)
    {
        if (stateManager.CurrentState == PlayerState.Dead)
        {
            return;
        }

        if (stateManager.CurrentState == PlayerState.Attack)
        {
            return;
        }

        if (!isGrounded)
        {
            stateManager.ChangeState(PlayerState.Jump);
            return;
        }

        if (input.MoveInput.sqrMagnitude > 0.01f)
        {
            stateManager.ChangeState(PlayerState.Move);
        }
        else
        {
            stateManager.ChangeState(PlayerState.Idle);
        }
    }

    public void EndAttackState()
    {
        basicAttackActive = false;

        if (stateManager == null || stateManager.CurrentState == PlayerState.Dead)
        {
            return;
        }

        bool isGrounded = movement != null && movement.CheckGrounded();
        bool moving = input != null && input.MoveInput.sqrMagnitude > 0.01f;

        if (!isGrounded)
        {
            stateManager.ChangeState(PlayerState.Jump);
        }
        else if (moving)
        {
            stateManager.ChangeState(PlayerState.Move);
        }
        else
        {
            stateManager.ChangeState(PlayerState.Idle);
        }
    }
}
