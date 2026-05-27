using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private PlayerInput input;
    private PlayerMovement movement;
    private Animator anim; // 추가: 애니메이터를 담을 변수

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        input = GetComponent<PlayerInput>();
        movement = GetComponent<PlayerMovement>();
        
        // Animator 컴포넌트를 찾아오기
        anim = GetComponentInChildren<Animator>(); 
    }

    // Update is called once per frame
    void Update()
    {
        // 이동 로직 실행
        movement.Move(input.MoveInput);

        // 애니메이션 속도 파라미터 전달
        if (anim != null)
        {
            // 입력값의 크기 계산. 가만히 있으면 0, 움직이면 0보다 큰 값이 Animator의 "MoveSpeed"로 넘어감
            anim.SetFloat("MoveSpeed", input.MoveInput.magnitude);
        }
    }
}