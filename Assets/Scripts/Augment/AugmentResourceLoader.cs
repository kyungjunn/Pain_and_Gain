using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum AugmentSessionState
{
    Idle,
    Starting,
    Active,
    Ending
}

public enum AugmentApplyStatus
{
    Applied,
    Stacked,
    Failed,
    Stale,
    AlreadyMax,
    Invalid
}

// 선택한 스킬 프리팹 요청, 전투 캐시, 세션 토큰, 종료 drain만 소유하는 전용 호스트.
public class AugmentResourceLoader : MonoBehaviour
{
    private static AugmentResourceLoader instance;
    private static bool isQuitting;

    private readonly List<AugmentSO> catalog = new List<AugmentSO>();
    private readonly HashSet<AugmentSO> failedDefinitions = new HashSet<AugmentSO>();
    private readonly Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, ResourceRequest> inFlight = new Dictionary<string, ResourceRequest>();
    private readonly List<ResourceRequest> drainingRequests = new List<ResourceRequest>();

    private AugmentSessionState state = AugmentSessionState.Idle;
    private int sessionId;
    private int nextSessionId = 1;
    private Scene inGameScene;
    private Scene mapScene;
    private GameObject currentPlayer;
    private Coroutine endRoutine;
    private bool metadataReady;

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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static AugmentResourceLoader EnsureInstance()
    {
        if (isQuitting)
        {
            return instance;
        }

        if (instance != null)
        {
            return instance;
        }

        AugmentResourceLoader existing = FindFirstObjectByType<AugmentResourceLoader>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            return existing;
        }

        GameObject host = new GameObject(nameof(AugmentResourceLoader));
        return host.AddComponent<AugmentResourceLoader>();
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
        metadataReady = false;
        sessionId = nextSessionId++;
        state = AugmentSessionState.Starting;
    }

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

        metadataReady = true;
        state = AugmentSessionState.Active;
        yield return null;
    }

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
        metadataReady = false;
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
