using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CharacterSpawnValidation
{
    private const string Pending = "CharacterSpawnValidation.Pending";

    static CharacterSpawnValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            var host = new GameObject("CharacterSpawnValidationRunner");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<CharacterSpawnValidationRunner>();
        };
    }

    public static void RunBatch()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Pending, true);
        EditorApplication.EnterPlaymode();
    }

    internal static object Field(object target, string name)
    {
        return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static IEnumerator Validate()
    {
        bool rejected = false;
        try { CharacterSelection.Confirm((PlayerCharacterId)99); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        Require(rejected, "Invalid character ID must be rejected");

        foreach (var id in new[] { PlayerCharacterId.Ninja, PlayerCharacterId.Sword, PlayerCharacterId.Wizard })
        {
            yield return SceneManager.LoadSceneAsync("SelectCharacter");
            yield return null;
            var selection = Object.FindFirstObjectByType<CharacterSelectManager>();
            Require(selection != null, "Selection manager missing");
            int button = id == PlayerCharacterId.Ninja ? 0 : id == PlayerCharacterId.Sword ? 1 : 2;
            var previousId = CharacterSelection.SelectedId;
            selection.SelectCharacter(button);
            Require(CharacterSelection.SelectedId == previousId, "Preview must not confirm selection");
            selection.OnClickStartButton();
            yield return null;
            float deadline = Time.realtimeSinceStartup + 45f;
            while (SpawnManager.Instance == null || SpawnManager.Instance.SpawnedPlayer == null)
            {
                Require(Time.realtimeSinceStartup < deadline, "Player spawn timed out: " + id);
                yield return null;
            }

            var manager = SpawnManager.Instance;
            var player = manager.SpawnedPlayer;
            Require(player.GetComponent<PlayerCharacter>().CharacterId == id, "Wrong character spawned");
            Require(SceneManager.GetSceneByName("Map").isLoaded, "Player spawned before map loaded");
            Require(GameObject.FindGameObjectsWithTag("Player").Length == 1, "Duplicate players");
            Require(GameObject.Find("IngameManager") == null && GameObject.Find("SpawnPoint") == null,
                "Obsolete scene spawning objects remain");
            var hud = Object.FindFirstObjectByType<CombatHudUI>();
            Require(hud != null && (PlayerSkillController)Field(hud, "skills") == player.GetComponent<PlayerSkillController>(),
                "Skill HUD is not bound to spawned player");
            int healthBars = 0;
            foreach (var bar in Object.FindObjectsByType<PlayerHealthBarUI>(FindObjectsSortMode.None))
            {
                if (!(bool)Field(bar, "bindSpawnedPlayer")) continue;
                healthBars++;
                Require((PlayerHealth)Field(bar, "playerHealth") == player.GetComponent<PlayerHealth>(), "Health HUD not bound");
            }
            Require(healthBars > 0, "No player HUD health bar found");
            var ending = Object.FindFirstObjectByType<GameEndHandler>();
            Require(ending != null && (PlayerStateManager)Field(ending, "stateManager") == player.GetComponent<PlayerStateManager>(),
                "Ending handler not bound");
            var spawn = typeof(SpawnManager).GetMethod("SpawnPlayer", BindingFlags.NonPublic | BindingFlags.Instance);
            spawn.Invoke(manager, null);
            Require(manager.SpawnedPlayer == player && GameObject.FindGameObjectsWithTag("Player").Length == 1,
                "Repeated spawn created another player");
            player.GetComponent<PlayerStateManager>().ChangeState(PlayerState.Dead);
            Require((bool)Field(ending, "ended"), "Death did not capture result");
            deadline = Time.realtimeSinceStartup + 10f;
            while (!SceneManager.GetSceneByName("EndingScene").isLoaded)
            {
                Require(Time.realtimeSinceStartup < deadline, "Ending scene timed out");
                yield return null;
            }
            Debug.Log("CHARACTER_SPAWN_PASS: " + id + " selection, map spawn, UI, duplicate guard, death and ending");
        }
    }
}

public sealed class CharacterSpawnValidationRunner : MonoBehaviour
{
    private IEnumerator Start()
    {
        var routine = CharacterSpawnValidation.Validate();
        while (true)
        {
            object current;
            try
            {
                if (!routine.MoveNext()) break;
                current = routine.Current;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                yield break;
            }
            yield return current;
        }
        Debug.Log("CHARACTER_SPAWN_VALIDATION_PASS");
        EditorApplication.Exit(0);
    }
}
