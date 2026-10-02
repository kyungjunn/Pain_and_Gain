using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class CharacterHudAutomation
{
    private const string Request = "Library/CharacterHud.request";
    private const string Result = "Library/CharacterHud.result";
    private static TestRunnerApi runner;

    static CharacterHudAutomation()
    {
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        string action = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        try
        {
            if (action == "build")
            {
                CharacterHudBuilder.Build();
                File.WriteAllText(Result, "BUILD PASSED\n");
            }
            else if (action == "test")
            {
                File.WriteAllText(Result, "TESTS RUNNING\n");
                runner = ScriptableObject.CreateInstance<TestRunnerApi>();
                runner.RegisterCallbacks(new Results());
                runner.Execute(new ExecutionSettings(new Filter
                {
                    testMode = TestMode.PlayMode,
                    testNames = new[] { "CharacterHudTests", "CombatHudTests", "CharacterHudIntegrationTests" }
                }));
            }
            else throw new ArgumentException("Unknown character HUD request: " + action);
        }
        catch (Exception e)
        {
            File.WriteAllText(Result, e.ToString());
            Debug.LogException(e);
        }
    }

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (!result.HasChildren)
                File.AppendAllText(Result, result.FullName + ": " + result.ResultState + "\n" + result.Message + "\n" + result.StackTrace + "\n");
        }
        public void RunFinished(ITestResultAdaptor result)
        {
            File.AppendAllText(Result, $"TOTAL passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount}\n");
        }
    }
}
