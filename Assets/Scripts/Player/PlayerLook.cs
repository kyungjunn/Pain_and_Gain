using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    public Transform cameraPivot; // 카메라 부모 (CameraPivot)
    public float sensitivity = 10f;
    public float upClamp = 35f;
    public float downClamp = -40f;

    private float xRotation = 0f;
    private PlayerInputHandler input;
    private PlayerStateManager stateManager;

    void Awake()
    {
        input = GetComponent<PlayerInputHandler>();
        stateManager = GetComponent<PlayerStateManager>();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (stateManager != null &&
            stateManager.CurrentState == PlayerState.Dead)
        {
            return;
        }

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