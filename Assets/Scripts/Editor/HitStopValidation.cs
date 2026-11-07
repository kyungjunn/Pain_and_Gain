using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class HitStopValidation
{
    [MenuItem("Tools/Combat/Validate Hit Stop Ownership")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run isolated hitstop tests in Edit Mode.");
        GameManager previousOwner = GameManager.Instance;
        float previousScale = Time.timeScale;
        bool previousCursor = Cursor.visible;
        CursorLockMode previousLock = Cursor.lockState;
        var root = new GameObject("HitStopValidation");
        root.SetActive(false);
        var player = new GameObject("HitStopValidationPlayer");
        player.SetActive(false);
        var health = player.AddComponent<PlayerHealth>();
        var game = root.AddComponent<GameManager>();
        try
        {
            GameManager.Instance = game;
            SetProperty(health, "CurrentHealth", 100);
            SetProperty(game, "SessionId", 123);
            Field("currentPlayer").SetValue(game, player);
            Time.timeScale = 1f;
            Require(game.RequestHitStop() && game.IsHitStopped && Time.timeScale == 0f, "active session accepts impact");
            double deadline = (double)Field("hitStopDeadline").GetValue(game);
            Require(Math.Abs(deadline - Time.unscaledTimeAsDouble - 0.05) < 0.001, "deadline is exactly 0.05 unscaled seconds");
            Require(!game.RequestHitStop() && (double)Field("hitStopDeadline").GetValue(game) == deadline,
                "overlapping impacts cannot extend deadline");
            game.PauseGame();
            Require(game.IsPaused && !game.IsHitStopped && Time.timeScale == 0f, "pause takes ownership");
            InvokeUpdate(game);
            Require(Time.timeScale == 0f, "expired hitstop cannot unpause menu");
            game.ResumeGame();
            Require(Time.timeScale == 1f && !game.IsHitStopped, "resume clears stop state");
            Require(game.RequestHitStop(), "new stop accepted");
            Field("hitStopDeadline").SetValue(game, Time.unscaledTimeAsDouble - 1.0);
            InvokeUpdate(game);
            Require(Time.timeScale == 1f && !game.IsHitStopped, "deadline releases time ownership");
            Require(game.RequestHitStop(), "stop accepted before player death");
            SetProperty(health, "CurrentHealth", 0);
            InvokeUpdate(game);
            Require(!game.IsHitStopped && Time.timeScale == 1f && !game.RequestHitStop(), "death clears stop and rejects new requests");
            SetProperty(health, "CurrentHealth", 100);
            Require(game.RequestHitStop(), "stop accepted before session invalidation");
            SetProperty(game, "SessionId", 0);
            InvokeUpdate(game);
            Require(!game.IsHitStopped && Time.timeScale == 1f && !game.RequestHitStop(), "ended session clears stop and rejects new requests");
            Debug.Log("HIT_STOP_VALIDATION_PASS");
        }
        finally
        {
            GameManager.Instance = previousOwner;
            Time.timeScale = previousScale;
            Cursor.visible = previousCursor;
            Cursor.lockState = previousLock;
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(root);
        }
    }

    private static FieldInfo Field(string name) => typeof(GameManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static void SetProperty(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    private static void InvokeUpdate(GameManager game) => typeof(GameManager).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, null);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("HIT_STOP_FAIL: " + message);
        Debug.Log("HIT_STOP_PASS: " + message);
    }
}
