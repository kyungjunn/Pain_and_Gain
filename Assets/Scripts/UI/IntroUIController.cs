using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class IntroUIController : MonoBehaviour
{
    [Header("버튼 컴포넌트 설정")]
    [SerializeField] private Button gameStartButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitButton;

    [Header("다음 씬 이름")]
    [SerializeField] private string nextSceneName = "InGame";

    private void Awake()
    {
        // 버튼 컴포넌트가 인스펙터에서 누락됐다면 자동으로 찾아주는 방어 코드
        if (gameStartButton == null) gameStartButton = transform.Find("GameStartButton")?.GetComponent<Button>();
        if (optionsButton == null) optionsButton = transform.Find("OptionsButton")?.GetComponent<Button>();
        if (quitButton == null) quitButton = transform.Find("QuitButton")?.GetComponent<Button>();
    }

    private void Start()
    {
        // 각 버튼에 클릭 리스너(이벤트) 연결
        if (gameStartButton != null)
            gameStartButton.onClick.AddListener(OnGameStartClicked);

        if (optionsButton != null)
            optionsButton.onClick.AddListener(OnOptionsClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);
    }

    private void OnGameStartClicked()
    {
        SceneManager.LoadScene(nextSceneName);
    }

    private void OnOptionsClicked()
    {
        Debug.Log("세팅(옵션) 창 열기 (추후 구현 예정)");
    }

    private void OnQuitClicked()
    {
        Debug.Log("게임 종료!");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에서 플레이 모드 종료
#else
        Application.Quit(); // 빌드된 게임 종료
#endif
    }
}