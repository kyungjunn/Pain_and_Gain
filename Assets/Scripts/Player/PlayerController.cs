using UnityEngine;

public class PlayerController : MonoBehaviour
{
    
    private PlayerInputHandler input;
    private PlayerMovement movement;
    private PlayerAttack playerAttack;
    private Animator anim;
    private PlayerStateManager stateManager;

    private void Awake()
    {
        EnsureCombatComponents();
    }

    private void Start()
    {
        input = GetComponent<PlayerInputHandler>();
        movement = GetComponent<PlayerMovement>();
        playerAttack = GetComponent<PlayerAttack>();
        anim = GetComponentInChildren<Animator>();
        stateManager = GetComponent<PlayerStateManager>();
    }

    private void Update()
    {
        if (stateManager.CurrentState == PlayerState.Dead)
        {
            return;
        }

        movement.Move(input.MoveInput);

        bool isGrounded = movement.CheckGrounded();

        UpdateMovementState(isGrounded);

        if (anim != null)
        {
            anim.SetFloat("MoveSpeed", input.MoveInput.magnitude);
            anim.SetFloat("LegSpeed", isGrounded ? 1f : 0f);
        }

        if (input.AttackTriggered)
        {
            stateManager.ChangeState(PlayerState.Attack);

            if (anim != null)
            {
                anim.SetTrigger("Attack");
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayPlayerAttack();
            }

            playerAttack?.TryAttack();
            input.AttackTriggered = false;
        }

        if (input.JumpTriggered)
        {
            movement.Jump();
            input.JumpTriggered = false;
        }
    }

    private void EnsureCombatComponents()
    {
        if (!TryGetComponent(out PlayerHealth _))
        {
            gameObject.AddComponent<PlayerHealth>();
        }

        if (!TryGetComponent(out playerAttack))
        {
            playerAttack = gameObject.AddComponent<PlayerAttack>();
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
        if (stateManager.CurrentState == PlayerState.Dead)
        {
            return;
        }

        bool isGrounded = movement.CheckGrounded();

        if (!isGrounded)
        {
            stateManager.ChangeState(PlayerState.Jump);
        }
        else if (input.MoveInput.sqrMagnitude > 0.01f)
        {
            stateManager.ChangeState(PlayerState.Move);
        }
        else
        {
            stateManager.ChangeState(PlayerState.Idle);
        }
    }
}
