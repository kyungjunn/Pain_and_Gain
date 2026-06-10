using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private PlayerInput input;
    private PlayerMovement movement;
    private PlayerAttack playerAttack;
    private Animator anim;

    private void Awake()
    {
        EnsureCombatComponents();
    }

    private void Start()
    {
        input = GetComponent<PlayerInput>();
        movement = GetComponent<PlayerMovement>();
        playerAttack = GetComponent<PlayerAttack>();
        anim = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        movement.Move(input.MoveInput);

        bool isGrounded = movement.CheckGrounded();

        if (anim != null)
        {
            anim.SetFloat("MoveSpeed", input.MoveInput.magnitude);
            anim.SetFloat("LegSpeed", isGrounded ? 1f : 0f);
        }

        if (input.AttackTriggered)
        {
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
}
