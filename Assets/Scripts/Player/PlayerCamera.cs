using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    [Header("따라다닐 대상 (플레이어를 넣어주세요)")]
    public Transform target;

    [Header("카메라 세팅")]
    public float distance = 5f; // 캐릭터와의 거리
    public float height = 2f;   // 캐릭터 머리 위 높이
    public float smoothSpeed = 10f; // 따라가는 속도 (낮을수록 부드럽지만 느림)

    void LateUpdate()
    {
        if (target == null) return;

        // 1. 캐릭터의 등 뒤(Back) 방향 벡터를 구합니다.
        Vector3 backDirection = -target.forward;

        // 2. 카메라가 위치해야 할 목표 지점을 계산합니다. (캐릭터 등 뒤로 distance만큼, 위로 height만큼 이동)
        Vector3 targetPosition = target.position + (backDirection * distance) + (Vector3.up * height);

        // 3. 현재 위치에서 목표 위치로 부드럽게 이동합니다. (Lerp 사용)
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);

        // 4. 카메라가 항상 캐릭터를 바라보도록 회전시킵니다.
        // 약간 위쪽(머리나 가슴)을 바라보게 하려면 target.position에 값을 더해주면 됩니다.
        Vector3 lookAtPosition = target.position + (Vector3.up * 1f); 
        transform.LookAt(lookAtPosition);
    }
}