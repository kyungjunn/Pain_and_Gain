using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using Unity.Cinemachine;
using Object = UnityEngine.Object;

public static class CombatFeedbackValidation
{
    public static IEnumerator Run()
    {
        GameManager game = GameManager.Instance;
        Require(game != null && game.IsActiveSession && game.CurrentPlayer != null, "active combat session");
        Require(!game.IsPaused, "session starts unpaused");
        var player = game.CurrentPlayer;
        var health = player.GetComponent<PlayerHealth>();
        var dealer = player.GetComponent<PlayerDamageDealer>();
        var feedback = player.GetComponent<PlayerCombatFeedback>();
        var numbers = player.GetComponent<CombatDamageNumbers>();
        Require(health != null && dealer != null && feedback != null && numbers != null, "feedback auto-wired on spawned player");
        Require(AudioManager.Instance != null && AudioManager.Instance.sfxSource != null &&
            AudioManager.Instance.playerHitClip != null && AudioManager.Instance.enemyHitClip != null, "real impact clips assigned");
        var target = new GameObject("CombatFeedbackValidationTarget");
        target.SetActive(false);
        target.transform.position = player.transform.position + player.transform.forward * 2f;
        var enemy = target.AddComponent<EnemyHealth>();
        Field(typeof(EnemyHealth), "expReward").SetValue(enemy, 0);
        target.SetActive(true);
        int damageEvents = 0;
        int capturedDamage = 0;
        Action<int> onDamage = value => { damageEvents++; capturedDamage = value; };
        health.OnDamaged += onDamage;
        int previousHealth = health.CurrentHealth;
        try
        {
            health.Heal(health.MaxHealth);
            int before = health.CurrentHealth;
            health.TakeDamage(10);
            Require(damageEvents == 1 && capturedDamage == before - health.CurrentHealth && capturedDamage > 0,
                "received damage reports actual post-defense health loss");
            Require((bool)Field(typeof(PlayerCombatFeedback), "damageFlashActive").GetValue(feedback), "player body flash starts on damage");
            Require(ActivePopups(numbers) > 0, "received damage creates popup");
            Require(game.IsHitStopped && Time.timeScale == 0f, "impact stops simulation");
            double deadline = (double)Field(typeof(GameManager), "hitStopDeadline").GetValue(game);
            Require(!game.RequestHitStop() && (double)Field(typeof(GameManager), "hitStopDeadline").GetValue(game) == deadline,
                "burst impacts do not extend active stop");
            float start = Time.unscaledTime;
            while (game.IsHitStopped) yield return null;
            Require(Time.unscaledTime - start >= 0.045f && Time.timeScale == 1f, "hitstop resumes after 0.05-second deadline");
            health.TakeDamage(0);
            health.Heal(1);
            Require(damageEvents == 1, "zero damage and healing do not emit damage events");
            Require(dealer.DealDamage(enemy, 7, PlayerDamageType.BasicAttack) == 7, "dealt damage uses actual enemy loss");
            Require(ActivePopups(numbers) >= 2, "dealt damage also creates popup");
            game.PauseGame();
            yield return new WaitForSecondsRealtime(0.1f);
            Require(game.IsPaused && !game.IsHitStopped && Time.timeScale == 0f, "menu pause supersedes hitstop without accidental resume");
            game.ResumeGame();
            Require(Time.timeScale == 1f && !game.IsHitStopped, "resume clears stale stop");
            yield return new WaitForSecondsRealtime(1.1f);
            Require(ActivePopups(numbers) == 0, "popups expire back into bounded pool");
            Require(!(bool)Field(typeof(PlayerCombatFeedback), "damageFlashActive").GetValue(feedback), "body flash restores original properties");
            var impulse = player.GetComponentInChildren<CinemachineImpulseSource>();
            Require(impulse != null, "camera impulse source exists");
            impulse.GenerateImpulseWithForce(0.1f);
            yield return new WaitForSeconds(0.08f);
            bool hasImpulse = CinemachineImpulseManager.Instance.GetImpulseAt(
                Camera.main.transform.position, false, impulse.ImpulseDefinition.ImpulseChannel,
                out Vector3 displacement, out Quaternion rotation);
            Require(hasImpulse && (displacement.sqrMagnitude > 0.000001f ||
                Quaternion.Angle(rotation, Quaternion.identity) > 0.001f),
                "camera shake emits a nonzero impulse signal");
            for (int i = 0; i < 40; i++) numbers.ShowDamage(i + 1, player.transform.position + Vector3.up, false);
            Require(ActivePopups(numbers) == 24, "popup saturation remains bounded at 24 entries");
            numbers.enabled = false;
            Require(Field(typeof(CombatDamageNumbers), "pool").GetValue(numbers) == null, "disable releases popup pool");
            numbers.enabled = true;
            Require(ActivePopups(numbers) == 0, "reenable starts with clean popups");
            Debug.Log("COMBAT_FEEDBACK_VALIDATION_PASS");
        }
        finally
        {
            health.OnDamaged -= onDamage;
            if (game.IsPaused) game.ResumeGame();
            if (health.CurrentHealth < previousHealth) health.Heal(previousHealth - health.CurrentHealth);
            Object.Destroy(target);
        }
    }

    private static int ActivePopups(CombatDamageNumbers numbers)
    {
        var pool = (Array)Field(typeof(CombatDamageNumbers), "pool").GetValue(numbers);
        if (pool == null) return 0;
        int count = 0;
        foreach (object popup in pool)
        {
            var rect = (RectTransform)popup.GetType().GetField("rect").GetValue(popup);
            if (rect.gameObject.activeSelf) count++;
        }
        return count;
    }
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("COMBAT_FEEDBACK_FAIL: " + message);
        Debug.Log("COMBAT_FEEDBACK_PASS: " + message);
    }
}
