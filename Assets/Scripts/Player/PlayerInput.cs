using UnityEngine;
using UnityEngine.InputSystem; // 새로운 Input System

public class PlayerInput : MonoBehaviour
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }

    public PlayerLevelSystem levelSystem;

    public void OnMove(InputValue value)
    {
        MoveInput = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        LookInput = value.Get<Vector2>();
    }

    // 경험치 획득 테스트 input (X)
    public void OnExpTest(InputValue value)
    {
        if (value.isPressed)
        {
            levelSystem.AddExp(25);
        }
    }
}
