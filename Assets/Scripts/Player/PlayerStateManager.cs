using UnityEngine;

public class PlayerStateManager : MonoBehaviour
{
    [SerializeField]
    private PlayerState currentState = PlayerState.Idle;

    public PlayerState CurrentState => currentState;

    public void ChangeState(PlayerState newState)
    {
        if (currentState == PlayerState.Dead)
        {
            return;
        }

        if (currentState == newState)
        {
            return;
        }

        currentState = newState;
    }
}