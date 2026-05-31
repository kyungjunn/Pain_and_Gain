using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public bool IsPaused { get; private set; }

    [Header("불러올 맵 씬 이름")]
    [SerializeField] private string mapSceneName = "Map";

    private void Awake()
    {
        // 싱글톤
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

    }
    private void Start()
    {
        StartCoroutine(LoadMapAndSpawnRoutine());
    }

    private IEnumerator LoadMapAndSpawnRoutine()
    {
        // UI Manager 에서 로딩 UI 시작
        //if (UIManager.Instance != null)
        //{
        //    UIManager.Instance.ShowLoadingUI(true);
        //}

        // 맵 씬을 비동기 + 추가 모드로 로드
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(mapSceneName, LoadSceneMode.Additive);

        // 맵 로딩이 끝날 때까지 대기
        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        // 맵을 주요 활성화 씬으로 설정
        Scene loadedScene = SceneManager.GetSceneByName(mapSceneName);
        SceneManager.SetActiveScene(loadedScene);

        // 스폰매니저의 스폰함수 호출
        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.SpawnAll();
        }

        // 로딩 완료 후 로딩 UI 닫기
        //if(UIManager.Instance != null)
        //{
        //    UIManager.Instance.ShowLoadingUI(false);
        //}
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
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }
}