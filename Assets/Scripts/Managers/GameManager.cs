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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        AugmentResourceLoader host = AugmentResourceLoader.Instance;
        host?.RequestEndCombatSession();
    }

    private IEnumerator LoadMapAndSpawnRoutine()
    {
        AugmentResourceLoader host = AugmentResourceLoader.EnsureInstance();
        if (host != null)
        {
            yield return host.EndCombatSession();
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowLoadingUI(true);
        }

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(mapSceneName, LoadSceneMode.Additive);
        asyncLoad.allowSceneActivation = false;

        while (asyncLoad.progress < 0.9f)
        {
            if (UIManager.Instance != null && UIManager.Instance.loadingPanelUI != null)
            {
                float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
                UIManager.Instance.loadingPanelUI.SetTargetProgress(progress);
            }
            yield return null;
        }

        if (UIManager.Instance != null && UIManager.Instance.loadingPanelUI != null)
        {
            UIManager.Instance.loadingPanelUI.SetTargetProgress(1f);
            while (!UIManager.Instance.loadingPanelUI.IsLoadingVisualDone)
            {
                yield return null;
            }
        }

        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
        {
            yield return null;
        }

        Scene loadedScene = SceneManager.GetSceneByName(mapSceneName);
        SceneManager.SetActiveScene(loadedScene);

        if (host != null)
        {
            host.BeginSession(gameObject.scene, loadedScene);
            yield return host.InitializeMetadata();
        }

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.SpawnAll();
        }

        yield return new WaitForSecondsRealtime(0.2f);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowLoadingUI(false);
        }
    }

    public void PauseGame()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        SetCursor(true);
    }

    public void ResumeGame()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        SetCursor(false);
    }

    public void SetCursor(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }
}
