using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float turnSmoothTime = 0.05f;

    private Rigidbody rb;
    private Vector3 movement;
    private float turnSmoothVelocity;
    private Transform mainCamera; 

    // ★ 추가 1: 애니메이터를 조종할 변수 선언
    private Animator animator; 

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; 
        
        // ★ 추가 2: 시작할 때 내 몸에 있는 Animator 컴포넌트를 찾아옵니다.
        animator = GetComponent<Animator>(); 
        
        if (Camera.main != null) mainCamera = Camera.main.transform;
    }

    void Update()
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.dKey.isPressed) horizontal = 1f;
            else if (Keyboard.current.aKey.isPressed) horizontal = -1f;

            if (Keyboard.current.wKey.isPressed) vertical = 1f;
            else if (Keyboard.current.sKey.isPressed) vertical = -1f;
        }

        if (mainCamera != null)
        {
            Vector3 camForward = mainCamera.forward;
            Vector3 camRight = mainCamera.right;

            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            movement = (camForward * vertical + camRight * horizontal).normalized;
        }
        else
        {
            movement = new Vector3(horizontal, 0f, vertical).normalized;
        }

        // ★ 추가 3: 애니메이터의 "Speed" 값에 내 이동 속도(movement 벡터의 길이)를 전달합니다!
        // 가만히 있으면 길이(magnitude)가 0, 움직이면 1이 들어갑니다.
        if (animator != null)
        {
            animator.SetFloat("Speed", movement.magnitude);
        }
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
        
        if (movement.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(movement.x, movement.z) * Mathf.Rad2Deg;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }
    }
}