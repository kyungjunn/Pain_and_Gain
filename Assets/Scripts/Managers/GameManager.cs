using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public bool IsPaused { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

    }

    public void PauseGame()
    {
        IsPaused = true;
        Time.timeScale = 0f;

        // 마우스 표시
        SetCursor(true);
    }

    public void ResumeGame()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        // 마우스 숨김, 고정
        SetCursor(false);
    }

    // 커서 설정
    public void SetCursor(bool visible)
    {
        Cursor.visible = visible;

        Cursor.lockState =
            visible
            ? CursorLockMode.None
            : CursorLockMode.Locked;
    }
}