using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    public Transform cameraPivot; // 카메라 부모 (CameraPivot)
    public float sensitivity = 30f;
    public float upClamp = 20f;
    public float downClamp = -30f;

    private float xRotation = 0f;
    private PlayerInput input;

    void Awake()
    {
        input = GetComponent<PlayerInput>();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        Look();
    }

    void Look()
    {
        Vector2 look = input.LookInput;

        float mouseX = look.x * sensitivity * Time.deltaTime;
        float mouseY = look.y * sensitivity * Time.deltaTime;

        // 상하 회전 (카메라)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, downClamp, upClamp);

        cameraPivot.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // 좌우 회전 (플레이어)
        transform.Rotate(Vector3.up * mouseX);
    }
}