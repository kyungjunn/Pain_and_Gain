using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(PlayerStateManager))]
public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f; // 플레이어 이동속도 (PlayerStats가 없을 때만 사용)
    public Transform cameraTransform;

    public float jumpForce = 5f;
    private Rigidbody rb;
    private PlayerStats stats;
    private PlayerStateManager stateManager;
    private PlayerSkillController skillController;
    private Vector3 desiredMovement;
    private bool jumpQueued;
    private bool controlsHorizontalVelocity;

    // 이동속도 증강/페널티가 실제 이동에 반영되도록 PlayerStats를 우선 사용
    private float MoveSpeed => stats != null ? stats.MoveSpeed : moveSpeed;

    // 현재 땅에 닿아있으면 true, 공중이면 false를 반환하는 함수
    public bool CheckGrounded()
    {
        // 캐릭터 중심에서 아래쪽으로 1.1f 길이의 레이저를 쏴서 땅이 있는지 확인합니다.
        return Physics.Raycast(transform.position, Vector3.down, 1.1f);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        stats = GetComponent<PlayerStats>();
        stateManager = GetComponent<PlayerStateManager>();
        skillController = GetComponent<PlayerSkillController>();
    }

    private bool IsMovementBlocked =>
        (GameManager.Instance != null && GameManager.Instance.IsPaused) ||
        Time.timeScale <= 0f ||
        stateManager.CurrentState == PlayerState.Dead ||
        (skillController != null && skillController.IsMovementLocked);

    private void OnDisable()
    {
        StopMovement();
    }

    private void FixedUpdate()
    {
        if (IsMovementBlocked)
        {
            StopMovement();
            return;
        }

        if (rb.isKinematic)
        {
            desiredMovement = Vector3.zero;
            jumpQueued = false;
            controlsHorizontalVelocity = false;
            return;
        }

        Vector3 velocity = desiredMovement * MoveSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;
        controlsHorizontalVelocity = true;

        if (jumpQueued)
        {
            jumpQueued = false;
            rb.linearVelocity = new Vector3(velocity.x, 0f, velocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    public void StopMovement()
    {
        desiredMovement = Vector3.zero;
        jumpQueued = false;

        // Release only the velocity owned by normal movement, not skill motion.
        if (controlsHorizontalVelocity && rb != null && !rb.isKinematic)
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

        controlsHorizontalVelocity = false;
    }

    public void Move(Vector2 input)
    {
        if (!isActiveAndEnabled || IsMovementBlocked)
        {
            StopMovement();
            return;
        }

        // 카메라 기준 방향
        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        // y값 제거 (땅 기준 이동)
        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        // 카메라 기준 이동 벡터 생성
        Vector3 move = forward * input.y + right * input.x;

        // 대각선 속도 보정
        desiredMovement = Vector3.ClampMagnitude(move, 1f);
    }

    public bool Jump()
    {
        if (isActiveAndEnabled && !IsMovementBlocked && !rb.isKinematic &&
            !jumpQueued && CheckGrounded())
        {
            // True means accepted; the impulse is consumed once by FixedUpdate.
            jumpQueued = true;
            return true;
        }

        return false;
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        jumpForce = Mathf.Max(0f, jumpForce);
    }
}
    