using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class IntroUIController : MonoBehaviour
{
    [Header("메인 메뉴 버튼")]
    [SerializeField] private Button gameStartButton;
    [SerializeField] private Button optionButton;
    [SerializeField] private Button quitButton;

    [Header("옵션 패널")]
    [SerializeField] private GameObject optionPanel;
    [SerializeField] private Button closeOptionsButton;

    private void Awake()
    {
        if (gameStartButton == null || optionButton == null || quitButton == null
            || optionPanel == null || closeOptionsButton == null)
        {
            Debug.LogError("IntroUIController requires all menu buttons and the options panel.", this);
            enabled = false;
            return;
        }

        optionPanel.SetActive(false);
    }

    private void OnEnable()
    {
        gameStartButton.onClick.AddListener(OnGameStartClicked);
        optionButton.onClick.AddListener(OnOptionClicked);
        quitButton.onClick.AddListener(OnQuitClicked);
        closeOptionsButton.onClick.AddListener(OnCloseOptionsClicked);
    }

    private void OnDisable()
    {
        if (gameStartButton != null) gameStartButton.onClick.RemoveListener(OnGameStartClicked);
        if (optionButton != null) optionButton.onClick.RemoveListener(OnOptionClicked);
        if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitClicked);
        if (closeOptionsButton != null) closeOptionsButton.onClick.RemoveListener(OnCloseOptionsClicked);
    }

    private void OnGameStartClicked()
    {
        SceneManager.LoadScene("SelectCharacter");
    }

    private void OnOptionClicked()
    {
        optionPanel.SetActive(true);
    }

    private void OnCloseOptionsClicked()
    {
        optionPanel.SetActive(false);
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
