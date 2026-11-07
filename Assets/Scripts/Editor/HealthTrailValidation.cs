using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class HealthTrailValidation
{
    public static IEnumerator Run()
    {
        var root = new GameObject("HealthTrailValidationPlayer");
        root.SetActive(false);
        root.transform.position = Vector3.one * 1000f;
        var health = root.AddComponent<PlayerHealth>();
        var bar = new GameObject("Bar", typeof(RectTransform), typeof(Slider));
        bar.transform.SetParent(root.transform, false);
        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bar.transform, false);
        var slider = bar.GetComponent<Slider>();
        slider.fillRect = (RectTransform)fill.transform;
        var ui = bar.AddComponent<PlayerHealthBarUI>();
        Set(ui, "slider", slider);
        var enemyObject = new GameObject("HealthTrailValidationEnemy");
        enemyObject.SetActive(false);
        enemyObject.transform.position = Vector3.one * 1000f;
        var enemy = enemyObject.AddComponent<EnemyHealth>();
        Set(enemy, "maxHealth", 100);
        Set(enemy, "expReward", 0);
        var enemyBar = enemyObject.AddComponent<EnemyHealthBar>();
        var bossBar = enemyObject.AddComponent<BossHealthBar>();
        var game = GameManager.Instance;
        bool pausedByTest = false;
        try
        {
            root.SetActive(true);
            enemyObject.SetActive(true);
            yield return null;
            var trail = (HealthDamageTrail)Get(ui, "damageTrail");
            Empty(trail, "full health has no white fill");
            health.TakeDamage(25);
            Require(Mathf.Approximately(slider.value, 0.75f), "player red health updates immediately");
            Segment(trail, 0.75f, 1f, "only lost 25 percent is white");
            health.TakeDamage(10);
            Segment(trail, 0.65f, 1f, "consecutive damage combines only the lost interval");
            if (game != null && !game.IsPaused)
            {
                game.PauseGame();
                pausedByTest = true;
                yield return new WaitForSecondsRealtime(0.25f);
                Segment(trail, 0.65f, 1f, "menu pause holds loss segment");
                game.ResumeGame();
                pausedByTest = false;
            }
            yield return new WaitForSecondsRealtime(0.25f);
            Empty(trail, "white loss segment disappears within 0.18 seconds");
            Require(Mathf.Approximately(slider.value, 0.65f), "only actual red health remains after white disappears");
            health.TakeDamage(10);
            health.Heal(5);
            Empty(trail, "healing clears stale white segment");
            typeof(PlayerHealth).GetField("maxHealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(health, 120);
            health.onHealthChanged?.Invoke();
            Empty(trail, "max health changes never create fake white damage");
            ui.enabled = false;
            Empty(trail, "unbinding clears damage");
            ui.enabled = true;
            Empty(trail, "rebinding shows actual health without damage");

            var enemyTrail = (HealthDamageTrail)Get(enemyBar, "damageTrail");
            var bossTrail = (HealthDamageTrail)Get(bossBar, "damageTrail");
            enemy.TakeDamage(20);
            Segment(enemyTrail, 0.8f, 1f, "hidden enemy bar shows only fresh damage");
            Segment(bossTrail, 0.8f, 1f, "boss uses the same white loss interval");
            var enemyFill = (RectTransform)Get(enemyBar, "fillRect");
            var bossFill = (RectTransform)Get(bossBar, "currentFillRect");
            Require(Mathf.Abs(enemyFill.sizeDelta.x - 1.4f * 0.8f) < 0.001f &&
                Mathf.Abs(bossFill.sizeDelta.x - 594f * 0.8f) < 0.001f, "enemy and boss red fills immediately reflect actual health");
            yield return new WaitForSecondsRealtime(0.25f);
            Empty(enemyTrail, "enemy white segment clears quickly");
            Empty(bossTrail, "boss white segment clears quickly");
            ((Canvas)Get(enemyBar, "canvas")).gameObject.SetActive(false);
            enemy.TakeDamage(10);
            Segment(enemyTrail, 0.7f, 0.8f, "enemy bar retains health history after hiding");
            enemy.TakeDamage(1000);
            Segment(enemyTrail, 0f, 0.8f, "lethal damage briefly shows remaining lost health");
            yield return new WaitForSecondsRealtime(0.25f);
            Empty(enemyTrail, "lethal loss segment also clears");
            Empty(bossTrail, "boss death does not leave white health");
            Debug.Log("HEALTH_TRAIL_VALIDATION_PASS");
        }
        finally
        {
            if (pausedByTest && game != null) game.ResumeGame();
            Object.Destroy(root);
            Object.Destroy(enemyObject);
        }
    }
    private static void Segment(HealthDamageTrail trail, float start, float end, string message)
    {
        var image = trail.GetComponentInChildren<Image>(true);
        Require(image.enabled && image.color == Color.white &&
            Mathf.Approximately(image.rectTransform.anchorMin.x, start) &&
            Mathf.Approximately(image.rectTransform.anchorMax.x, end), message);
    }
    private static void Empty(HealthDamageTrail trail, string message) =>
        Require(!trail.GetComponentInChildren<Image>(true).enabled, message);
    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("HEALTH_TRAIL_FAIL: " + message);
        Debug.Log("HEALTH_TRAIL_PASS: " + message);
    }
}
