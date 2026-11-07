using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CombatQuestValidation
{
    public static string Status { get; private set; } = "Not run";
    public static void Start()
    {
        if (!Application.isPlaying || GameManager.Instance == null || GameManager.Instance.CurrentPlayer == null)
            throw new InvalidOperationException("Start tests in a loaded InGame Play Mode session.");
        if (Status == "Running") throw new InvalidOperationException("Validation already running.");
        SpawnManager.Instance.StopCombatSpawning();
        foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
            enemy.gameObject.SetActive(false);
        UIManager.Instance.ClearSession(true);
        Status = "Running";
        GameManager.Instance.StartCoroutine(Run());
    }
    private static IEnumerator Run()
    {
        var stack = new Stack<IEnumerator>();
        var tests = new[] { HealthTrailValidation.Run(), CombatFeedbackValidation.Run(), QuestFlowValidation.Run() };
        foreach (var test in tests)
        {
            stack.Push(test);
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                bool moved = false;
                object yielded = null;
                Exception failure = null;
                try { moved = current.MoveNext(); if (moved) yielded = current.Current; }
                catch (Exception exception) { failure = exception; }
                if (failure != null)
                {
                    while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
                    Status = "Failed: " + failure;
                    Debug.LogError(Status);
                    yield break;
                }
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (yielded is IEnumerator nested) { stack.Push(nested); continue; }
                yield return yielded;
            }
        }
        Status = "Passed";
        Debug.Log("COMBAT_QUEST_VALIDATION_PASS");
    }
}
