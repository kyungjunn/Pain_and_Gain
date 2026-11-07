using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class AugmentTeardownValidation
{
    public static string LiveStatus { get; private set; } = "Not run";

    public static IEnumerator RunLive()
    {
        LiveStatus = "Running";
        var manager = GameManager.Instance;
        Require(Application.isPlaying && manager != null && manager.IsActiveSession && manager.CurrentPlayer != null,
            "live fixture has an active player session");
        var loader = AugmentResourceLoader.Instance;
        Require(loader != null && loader.Catalog.Count > 0, "live loader has initialized metadata");
        SpawnManager.Instance.StopCombatSpawning();
        foreach (var enemy in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
            enemy.gameObject.SetActive(false);
        int sessionId = manager.SessionId;
        var player = manager.CurrentPlayer;
        manager.EndCombatSession(sessionId);
        Require(manager.SessionId == 0 && ReferenceEquals(Get(manager, "resourceLoader"), null) && loader.IsReleasing,
            "normal shutdown detaches loader and starts its live release coroutine");
        double deadline = Time.unscaledTimeAsDouble + 30;
        while (loader.IsReleasing)
        {
            if (Time.unscaledTimeAsDouble >= deadline)
                throw new InvalidOperationException("AUGMENT_TEARDOWN_FAIL: resource release timed out");
            yield return null;
        }
        Require(loader.Catalog.Count == 0 && Time.timeScale == 1f, "normal release clears metadata without freezing time");
        manager.NotifyPlayerUnavailable(player);
        manager.EndCombatSession(sessionId);
        Require(manager.SessionId == 0 && !loader.IsReleasing, "late player notification cannot restart cleanup");
        LiveStatus = "Passed";
        Debug.Log("AUGMENT_TEARDOWN_LIVE_PASS");
    }

    [MenuItem("Tools/Augment/Validate Teardown")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run isolated teardown tests in Edit Mode.");
        var previousGame = GameManager.Instance;
        var previousSpawn = SpawnManager.Instance;
        var previousUi = UIManager.Instance;
        var previousAugment = AugmentManager.Instance;
        var previousQuest = QuestManager.Instance;
        float previousScale = Time.timeScale;
        bool previousCursor = Cursor.visible;
        CursorLockMode previousLock = Cursor.lockState;
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            Singleton(typeof(SpawnManager), null);
            Singleton(typeof(UIManager), null);
            Singleton(typeof(AugmentManager), null);
            Singleton(typeof(QuestManager), null);
            var loaderObject = Inactive("DestroyedLoader", scene);
            var loader = loaderObject.AddComponent<AugmentResourceLoader>();
            Set(loader, "ready", true);
            var player = Inactive("TeardownPlayer", scene);
            var playerAugments = player.AddComponent<PlayerAugments>();
            var manager = Inactive("TeardownManager", scene).AddComponent<GameManager>();
            GameManager.Instance = manager;
            typeof(GameManager).GetProperty("SessionId").SetValue(manager, 901);
            Set(manager, "currentPlayer", player);
            Set(manager, "resourceLoader", loader);
            int endEvents = 0;
            manager.OnCombatSessionEnding += id =>
            {
                endEvents++;
                Require(id == 901 && !manager.IsActiveSession, "ending callback sees invalidated session");
                Require(ReferenceEquals(Get(manager, "resourceLoader"), null), "cached loader detached before callbacks");
                manager.EndCombatSession(id);
            };
            Object.DestroyImmediate(loaderObject);
            Require(loader == null && !ReferenceEquals(loader, null), "fixture reproduces destroyed Unity wrapper");
            loader.ReleaseResources();
            Require(ReferenceEquals(Get(manager, "resourceLoader"), loader), "manager still holds the original destroyed wrapper");
            Invoke(playerAugments, "OnDestroy");
            Require(manager.SessionId == 0 && manager.CurrentPlayer == null && endEvents == 1,
                "player destruction safely ends session once after loader was destroyed");
            Require(ReferenceEquals(Get(manager, "resourceLoader"), null) && Time.timeScale == 1f,
                "teardown clears cached reference and restores simulation time");
            manager.EndCombatSession(901);
            Require(endEvents == 1, "duplicate shutdown is harmless");

            var quittingLoader = Inactive("QuittingLoader", scene).AddComponent<AugmentResourceLoader>();
            Set(quittingLoader, "ready", true);
            Invoke(quittingLoader, "OnApplicationQuit");
            quittingLoader.ReleaseResources();
            Require(!quittingLoader.IsReleasing && (bool)Get(quittingLoader, "ready"),
                "application quit does not start a release coroutine");
            var secondManager = Inactive("QuittingManager", scene).AddComponent<GameManager>();
            GameManager.Instance = secondManager;
            typeof(GameManager).GetProperty("SessionId").SetValue(secondManager, 902);
            Set(secondManager, "resourceLoader", quittingLoader);
            secondManager.EndCombatSession(902);
            Require(secondManager.SessionId == 0 && ReferenceEquals(Get(secondManager, "resourceLoader"), null) &&
                !quittingLoader.IsReleasing, "quit teardown still detaches the live loader without scheduling work");

            Object.DestroyImmediate(secondManager.gameObject);
            GameManager.Instance = secondManager;
            Require(GameManager.Instance == null && !ReferenceEquals(GameManager.Instance, null),
                "fixture also retains a destroyed manager wrapper");
            Invoke(playerAugments, "OnDestroy");
            Debug.Log("AUGMENT_TEARDOWN_VALIDATION_PASS");
        }
        finally
        {
            GameManager.Instance = previousGame;
            Singleton(typeof(SpawnManager), previousSpawn);
            Singleton(typeof(UIManager), previousUi);
            Singleton(typeof(AugmentManager), previousAugment);
            Singleton(typeof(QuestManager), previousQuest);
            EditorSceneManager.ClosePreviewScene(scene);
            Time.timeScale = previousScale;
            Cursor.visible = previousCursor;
            Cursor.lockState = previousLock;
        }
    }

    private static GameObject Inactive(string name, Scene scene)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }
    private static void Singleton(Type type, object value)
    {
        FieldInfo field = type.GetField("Instance");
        if (field != null) field.SetValue(null, value);
        else type.GetProperty("Instance").SetValue(null, value);
    }
    private static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("AUGMENT_TEARDOWN_FAIL: " + message);
        Debug.Log("AUGMENT_TEARDOWN_PASS: " + message);
    }
}
