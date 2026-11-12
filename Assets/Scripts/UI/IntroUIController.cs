using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
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
        SelectButton(gameStartButton);
    }

    private void Start()
    {
        SelectButton(gameStartButton);
    }

    private void Update()
    {
        if (!optionPanel.activeSelf) return;

        var inputModule = EventSystem.current != null
            ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;
        if (inputModule != null && inputModule.cancel != null
            && inputModule.cancel.action != null && inputModule.cancel.action.WasPressedThisFrame())
        {
            OnCloseOptionsClicked();
        }
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
        gameStartButton.interactable = false;
        optionButton.interactable = false;
        quitButton.interactable = false;
        optionPanel.SetActive(true);
        SelectButton(closeOptionsButton);
    }

    private void OnCloseOptionsClicked()
    {
        optionPanel.SetActive(false);
        gameStartButton.interactable = true;
        optionButton.interactable = true;
        quitButton.interactable = true;
        SelectButton(optionButton);
    }

    private static void SelectButton(Button button)
    {
        if (EventSystem.current != null && button != null
            && button.IsActive() && button.IsInteractable())
        {
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
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
