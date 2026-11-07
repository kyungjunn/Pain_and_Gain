using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

// 전투 세션 소유자. 시작·종료·현재 플레이어.
public class GameManager : MonoBehaviour
{
    private const float HitStopDuration = 0.05f;

    public static GameManager Instance;
    private static int nextSessionId = 1;
    private AugmentResourceLoader resourceLoader;
    private Scene mapScene;
    private GameObject currentPlayer;
    private AsyncOperation pendingMapLoad;
    private bool startupCancelled;
    private double hitStopDeadline;
    private float timeScaleBeforeHitStop = 1f;
    private int hitStopSessionId;

    public int SessionId { get; private set; }
    public GameObject CurrentPlayer => currentPlayer;
    public bool IsActiveSession => Instance == this && SessionId != 0;

    public bool IsPaused { get; private set; }
    public bool IsHitStopped { get; private set; }
    public event Action<int> OnCombatSessionEnding;

    [Header("불러올 맵 씬 이름")]
    [SerializeField] private string mapSceneName = "Map";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        nextSessionId = 1;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            GameResult.Clear();
            RunStats.Reset();
            Time.timeScale = 1f;
            IsPaused = false;
            CinemachineImpulseManager.Instance.Clear();
            CinemachineImpulseManager.Instance.IgnoreTimeScale = true;
        }
        else
        {
            // 중복: 기존 세션 유지
            enabled = false;
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        if (Instance != this) return;
        SpawnManager.OnPlayerSpawned += BindPlayer;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    private void OnDisable()
    {
        SpawnManager.OnPlayerSpawned -= BindPlayer;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        CancelStartup();
        if (Instance == this)
        {
            ClearHitStop(false);
            EndCombatSession(SessionId);
        }
    }

    private void Start()
    {
        if (Instance != this || startupCancelled) return;
        StartCoroutine(LoadMapAndSpawnRoutine());
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            CancelStartup();
            EndCombatSession(SessionId);
            Instance = null;
        }
    }

    private void Update()
    {
        if (!IsHitStopped)
            return;

        if (IsPaused)
        {
            ClearHitStop(false);
            Time.timeScale = 0f;
            return;
        }

        if (!BelongsToSession(hitStopSessionId) || IsCurrentPlayerDeadOrUnavailable())
        {
            ClearHitStop(true);
            return;
        }

        if (Time.unscaledTimeAsDouble >= hitStopDeadline)
            ClearHitStop(true);
    }

    // 토큰 일치 + 활성 세션
    public bool BelongsToSession(int expectedSessionId)
    {
        return IsActiveSession && SessionId == expectedSessionId;
    }

    private void CancelStartup()
    {
        startupCancelled = true;
        // 씬 로드 취소 불가. 활성화 잠금만 해제.
        if (pendingMapLoad != null) pendingMapLoad.allowSceneActivation = true;
        pendingMapLoad = null;
    }

    // 자기 세션만 종료
    public void EndCombatSession(int expectedSessionId)
    {
        if (!BelongsToSession(expectedSessionId)) return;
        int endingSessionId = SessionId;
        SessionId = 0; // 먼저 무효화해서 파괴 알림의 재진입을 차단한다.
        AugmentResourceLoader loader = resourceLoader;
        resourceLoader = null;
        GameObject player = currentPlayer;
        currentPlayer = null;
        ClearHitStop(false);
        OnCombatSessionEnding?.Invoke(endingSessionId);
        CinemachineImpulseManager.Instance.Clear();
        if (SpawnManager.Instance != null) SpawnManager.Instance.StopCombatSpawning();
        if (UIManager.Instance != null) UIManager.Instance.ClearSession(false);
        if (AugmentManager.Instance != null) AugmentManager.Instance.ClearSession();
        if (QuestManager.Instance != null) QuestManager.Instance.ClearSession();
        if (player != null && player.TryGetComponent(out PlayerAugments augments))
            augments.ReleaseAll();
        if (loader != null) loader.ReleaseResources();
        IsPaused = false;
        Time.timeScale = 1f;
        SetCursor(true);
    }

    // 소유 플레이어 소실 → 세션 종료
    public void NotifyPlayerUnavailable(GameObject player)
    {
        if (ReferenceEquals(currentPlayer, player))
            EndCombatSession(SessionId);
    }

    private void BindPlayer(GameObject player)
    {
        if (!IsActiveSession || ReferenceEquals(currentPlayer, player)) return;
        // 교체 = 새 세션. 이전 적용 권한 차단.
        if (!ReferenceEquals(currentPlayer, null))
        {
            EndCombatSession(SessionId);
            return;
        }
        currentPlayer = player;
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        if (scene.handle == mapScene.handle || scene.handle == gameObject.scene.handle)
            EndCombatSession(SessionId);
    }

    private IEnumerator LoadMapAndSpawnRoutine()
    {
        resourceLoader = AugmentResourceLoader.Instance;
        if (resourceLoader == null)
        {
            Debug.LogError("전투 시작에 AugmentResourceLoader가 필요합니다.", this);
            yield break;
        }
        yield return resourceLoader.WaitForRelease();
        if (Instance != this || !isActiveAndEnabled || startupCancelled) yield break;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowLoadingUI(true);
        }

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(mapSceneName, LoadSceneMode.Additive);
        pendingMapLoad = asyncLoad;
        asyncLoad.allowSceneActivation = false;

        // 활성화 대기 구간 제외: 실제 로딩 진행률 정규화
        while (asyncLoad.progress < 0.9f)
        {
            if (startupCancelled) yield break;
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
                if (startupCancelled) yield break;
                yield return null;
            }
        }

        asyncLoad.allowSceneActivation = true;

        while (!asyncLoad.isDone)
        {
            yield return null;
        }
        pendingMapLoad = null;

        if (Instance != this || !isActiveAndEnabled || startupCancelled) yield break;
        Scene loadedScene = SceneManager.GetSceneByName(mapSceneName);
        SceneManager.SetActiveScene(loadedScene);

        mapScene = loadedScene;
        resourceLoader.InitializeMetadata();
        // 맵 준비 후 세션 발급: 생성 콜백의 적용 권한 확보
        SessionId = nextSessionId++;

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.SpawnAll();
            BindPlayer(SpawnManager.Instance.SpawnedPlayer);
        }
        if (currentPlayer == null)
        {
            EndCombatSession(SessionId);
            Debug.LogError("플레이어 생성에 실패하여 전투 세션을 종료합니다.", this);
            yield break;
        }

        yield return new WaitForSecondsRealtime(0.2f);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowLoadingUI(false);
        }
    }

    public void PauseGame()
    {
        bool enteringPause = !IsPaused;
        IsPaused = true;
        ClearHitStop(false);
        Time.timeScale = 0f;
        SetCursor(true);
        if (enteringPause && AudioManager.Instance != null) AudioManager.Instance.PlayPause();
    }

    public void ResumeGame()
    {
        bool leavingPause = IsPaused;
        ClearHitStop(false);
        IsPaused = false;
        Time.timeScale = 1f;
        SetCursor(false);
        if (leavingPause && AudioManager.Instance != null) AudioManager.Instance.PlayResume();
    }

    public bool RequestHitStop()
    {
        if (!IsActiveSession || IsPaused || IsHitStopped || Time.timeScale <= 0f ||
            IsCurrentPlayerDeadOrUnavailable())
            return false;

        hitStopSessionId = SessionId;
        timeScaleBeforeHitStop = Time.timeScale;
        hitStopDeadline = Time.unscaledTimeAsDouble + HitStopDuration;
        IsHitStopped = true;
        Time.timeScale = 0f;
        return true;
    }

    private bool IsCurrentPlayerDeadOrUnavailable()
    {
        if (currentPlayer == null || !currentPlayer.TryGetComponent(out PlayerHealth health))
            return true;

        return health.IsDead || health.CurrentHealth <= 0;
    }

    private void ClearHitStop(bool restoreTimeScale)
    {
        if (!IsHitStopped)
            return;

        IsHitStopped = false;
        hitStopDeadline = 0f;
        hitStopSessionId = 0;

        if (restoreTimeScale && !IsPaused && Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = timeScaleBeforeHitStop;
    }

    public void SetCursor(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }
}
