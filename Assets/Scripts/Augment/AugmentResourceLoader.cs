using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 증강 시스템의 생명주기
public enum AugmentSessionState
{
    Idle,       // 전투 X
    Starting,   // 전투 준비
    Active,     // 전투 진행
    Ending      // 전투 끝
}

// 증강 적용 결과
public enum AugmentApplyStatus
{
    Applied,        // 증강 적용
    Stacked,        // 스킬 중첩 증가
    Failed,         // 적용 실패
    Stale,          // 기간 만료 요청
    AlreadyMax,     // 중첩 최대치
    Invalid         // 잘못된 요청
}

// 선택한 스킬 프리팹 요청, 전투 캐시, 세션 토큰, 종료 drain만 소유하는 전용 호스트.
public class AugmentResourceLoader : MonoBehaviour
{
    // 영구 호스트
    private static AugmentResourceLoader instance;
    private static bool isQuitting;

    // 증강 목록
    private readonly List<AugmentSO> catalog = new List<AugmentSO>();
    // 전투 제외 목록
    private readonly HashSet<AugmentSO> failedDefinitions = new HashSet<AugmentSO>();
    // 프리팹 캐시
    private readonly Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();
    // 로딩 요청
    private readonly Dictionary<string, ResourceRequest> inFlight = new Dictionary<string, ResourceRequest>();
    // 종료 대기 요청
    private readonly List<ResourceRequest> drainingRequests = new List<ResourceRequest>();

    // 세션 정보
    private AugmentSessionState state = AugmentSessionState.Idle;
    private int sessionId;
    private int nextSessionId = 1;
    private Scene inGameScene;
    private Scene mapScene;
    private GameObject currentPlayer;
    private Coroutine endRoutine;

    public static AugmentResourceLoader Instance => instance;

    public int SessionId => sessionId;
    public AugmentSessionState State => state;
    public GameObject CurrentPlayer => currentPlayer;
    public IReadOnlyList<AugmentSO> Catalog => catalog;
    public bool IsActiveSession => state == AugmentSessionState.Active;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
        isQuitting = false;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;

        if (instance == this)
        {
            instance = null;
        }
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    // 전투 세션 시작
    public void BeginSession(Scene inGame, Scene map)
    {
        if (state != AugmentSessionState.Idle)
        {
            Debug.LogError("이전 전투 종료가 끝나기 전에 새 세션을 시작할 수 없습니다.");
            return;
        }

        inGameScene = inGame;
        mapScene = map;
        currentPlayer = null;
        catalog.Clear();
        failedDefinitions.Clear();
        prefabCache.Clear();
        inFlight.Clear();
        sessionId = nextSessionId++;
        state = AugmentSessionState.Starting;
    }

    // 증강 메타데이터 로드
    public IEnumerator InitializeMetadata()
    {
        if (state != AugmentSessionState.Starting)
        {
            yield break;
        }

        catalog.Clear();
        HashSet<string> usedPaths = new HashSet<string>();
        AugmentSO[] loaded = Resources.LoadAll<AugmentSO>("Augments/Data");
        Array.Sort(loaded, (a, b) => string.CompareOrdinal(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty));

        for (int i = 0; i < loaded.Length; i++)
        {
            AugmentSO augment = loaded[i];
            if (augment == null)
            {
                continue;
            }

            if (augment is SkillAugmentSO skill)
            {
                string path = skill.NormalizedResourcePath();
                if (string.IsNullOrEmpty(path))
                {
                    Debug.LogError($"[Augment] 스킬 경로가 비어 있어 제외합니다: {skill.name}");
                    continue;
                }

                if (!usedPaths.Add(path))
                {
                    Debug.LogError($"[Augment] 중복 스킬 경로를 제외합니다: {skill.name} -> {path}");
                    continue;
                }
            }

            catalog.Add(augment);
        }

        state = AugmentSessionState.Active;
        yield return null;
    }

    // 플레이어 연결
    public void BindPlayer(GameObject player)
    {
        if (state == AugmentSessionState.Active)
        {
            currentPlayer = player;
        }
    }

    public void NotifyPlayerReplaced(GameObject oldPlayer)
    {
        if (oldPlayer != null && oldPlayer == currentPlayer)
        {
            RequestEndCombatSession();
        }
    }

    public void NotifyPlayerUnavailable(GameObject player)
    {
        // 이 플레이어(로더에 연결된)가 니 플레이어가 맞으면
        if (player != null && player == currentPlayer)
        {
            RequestEndCombatSession();
        }
    }

    public bool BelongsToSession(int expectedSessionId)
    {
        return state == AugmentSessionState.Active && sessionId == expectedSessionId;
    }

    public bool IsExcluded(AugmentSO augment)
    {
        return augment != null && failedDefinitions.Contains(augment);
    }

    public void ExcludeFromCombat(AugmentSO augment)
    {
        if (augment != null)
        {
            failedDefinitions.Add(augment);
        }
    }

    // 후보 목록 생성
    public List<AugmentSO> CreateCandidatePool(PlayerAugments playerAugments)
    {
        List<AugmentSO> pool = new List<AugmentSO>(catalog.Count);

        for (int i = 0; i < catalog.Count; i++)
        {
            AugmentSO augment = catalog[i];
            if (augment == null || failedDefinitions.Contains(augment))
            {
                continue;
            }

            if (augment is SkillAugmentSO skill && playerAugments != null && playerAugments.IsAtMaxStacks(skill))
            {
                continue;
            }

            pool.Add(augment);
        }

        return pool;
    }

    public bool TryGetCachedPrefab(string path, out GameObject prefab)
    {
        return prefabCache.TryGetValue(path, out prefab) && prefab != null;
    }

    // 스킬 프리팹 로드
    public IEnumerator LoadSkillPrefab(string path, int expectedSessionId, Action<GameObject, string> completed)
    {
        if (!BelongsToSession(expectedSessionId))
        {
            completed?.Invoke(null, null);
            yield break;
        }

        if (string.IsNullOrEmpty(path))
        {
            completed?.Invoke(null, "스킬 리소스 경로가 비어 있습니다.");
            yield break;
        }

        if (prefabCache.TryGetValue(path, out GameObject cached) && cached != null)
        {
            completed?.Invoke(cached, null);
            yield break;
        }

        if (!inFlight.TryGetValue(path, out ResourceRequest request) || request == null)
        {
            request = Resources.LoadAsync<GameObject>(path);
            inFlight[path] = request;
        }

        yield return request;
        inFlight.Remove(path);

        if (!BelongsToSession(expectedSessionId))
        {
            completed?.Invoke(null, null);
            yield break;
        }

        GameObject prefab = request.asset as GameObject;
        if (prefab == null || prefab.GetComponent<AugmentSkill>() == null)
        {
            completed?.Invoke(null, $"스킬 프리팹이 없거나 AugmentSkill이 없습니다: {path}");
            yield break;
        }

        prefabCache[path] = prefab;
        completed?.Invoke(prefab, null);
    }

    public IEnumerator EndCombatSession()
    {
        RequestEndCombatSession();
        while (state == AugmentSessionState.Ending)
        {
            yield return null;
        }
    }

    // 전투 세션 종료 요청
    public void RequestEndCombatSession()
    {
        if (isQuitting && instance == null)
        {
            return;
        }

        if (state == AugmentSessionState.Idle)
        {
            return;
        }

        if (endRoutine == null)
        {
            endRoutine = StartCoroutine(EndRoutine());
        }
    }

    // 로딩 정리 및 캐시 해제
    private IEnumerator EndRoutine()
    {
        state = AugmentSessionState.Ending;
        int endingSession = sessionId;
        sessionId = 0;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ClearSession(false);
        }

        if (currentPlayer != null && currentPlayer.TryGetComponent(out PlayerAugments playerAugments))
        {
            playerAugments.ReleaseAll();
        }

        currentPlayer = null;

        drainingRequests.Clear();
        foreach (KeyValuePair<string, ResourceRequest> pair in inFlight)
        {
            if (pair.Value != null)
            {
                drainingRequests.Add(pair.Value);
            }
        }

        inFlight.Clear();

        for (int i = 0; i < drainingRequests.Count; i++)
        {
            ResourceRequest request = drainingRequests[i];
            while (request != null && !request.isDone)
            {
                yield return null;
            }
        }

        drainingRequests.Clear();
        prefabCache.Clear();
        catalog.Clear();
        failedDefinitions.Clear();
        inGameScene = default;
        mapScene = default;

        AsyncOperation unload = Resources.UnloadUnusedAssets();
        if (unload != null)
        {
            yield return unload;
        }

        state = AugmentSessionState.Idle;
        endRoutine = null;
        _ = endingSession;
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        if (state != AugmentSessionState.Active)
        {
            return;
        }

        if (scene.handle == inGameScene.handle || scene.handle == mapScene.handle)
        {
            RequestEndCombatSession();
        }
    }
}
