using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class QuestFlowValidation
{
    public static IEnumerator Run()
    {
        var game = GameManager.Instance;
        var manager = QuestManager.Instance;
        var player = game.CurrentPlayer;
        var dealer = player.GetComponent<PlayerDamageDealer>();
        var skills = player.GetComponent<PlayerSkillController>();
        var level = player.GetComponent<PlayerLevelSystem>();
        const string key = "QuestManager.TutorialComplete.v1";
        bool hadHistory = PlayerPrefs.HasKey(key);
        int history = PlayerPrefs.GetInt(key);
        var target = new GameObject("QuestValidationTarget");
        target.SetActive(false);
        var enemy = target.AddComponent<EnemyHealth>();
        Set(enemy, "maxHealth", 10000);
        Set(enemy, "expReward", 0);
        target.transform.position = player.transform.position + player.transform.forward * 2f;
        target.SetActive(true);
        var normalDefinitions = (List<QuestSO>)Get(manager, "quests");
        int successes = 0, failures = 0;
        QuestPenaltyResult lastPenalty = default;
        Action<QuestSO, QuestRewardResult> success = (_, __) => successes++;
        Action<QuestSO, QuestPenaltyResult> failure = (_, result) => { failures++; lastPenalty = result; };
        manager.onQuestSucceeded += success;
        manager.onQuestFailed += failure;
        try
        {
            PlayerPrefs.SetInt(key, 0);
            manager.ClearSession();
            Invoke(manager, "HandlePlayerSpawned", player);
            yield return null;
            Require(manager.State == QuestState.Active && manager.CurrentQuest.category == QuestCategory.Tutorial &&
                manager.CurrentQuest.objectiveType == QuestObjectiveType.KillEnemies && manager.RemainingTime == 0f,
                "tutorial starts before first augment without a time limit");
            dealer.DealDamage(enemy, 1, PlayerDamageType.Skill, sourceSkill: skills.SkillE);
            Require((int)Get(manager, "runQSkillHitCount") == 0, "E does not count as Q");
            dealer.DealDamage(enemy, 1, PlayerDamageType.Skill, sourceSkill: skills.SkillQ);
            Require((int)Get(manager, "runQSkillHitCount") == 1, "actual equipped Q damage is remembered early");
            for (int i = 0; i < 3; i++) Invoke(manager, "HandleEnemyKilled", enemy);
            Require(manager.CurrentQuest.objectiveType == QuestObjectiveType.FirstAugment,
                "remembered Q skips completed Q step after kill objective");
            UIManager.Instance.ClearSession(true);
            var definition = Resources.Load<SkillAugmentSO>("Augments/Data/BasicAttackLifeSteal_Data");
            Require(definition != null, "stackable augment fixture exists");
            AugmentApplyStatus? applied = null;
            yield return AugmentManager.Instance.ApplyAugmentRoutine(definition, game.SessionId, 0, (status, _) => applied = status);
            Require(applied == AugmentApplyStatus.Applied || applied == AugmentApplyStatus.Stacked, "first augment acquired");
            Require(manager.CurrentQuest.objectiveType == QuestObjectiveType.HuntEventMonster, "first augment advances to practice hunt");
            var practice = (EnemyHealth)Get(manager, "activeEventMonster");
            Require(practice != null, "practice target spawned");
            KillWithoutExperience(practice);
            Require(PlayerPrefs.GetInt(key) == 1 && manager.State == QuestState.Countdown, "tutorial completion persisted");

            var normal = AssetDatabase.LoadAssetAtPath<QuestSO>("Assets/Prefabs/Quest/Data/KillEnemies_01.asset");
            Set(manager, "quests", new List<QuestSO> { normal });
            Invoke(manager, "StartNormalQuestPreview");
            Require(manager.State == QuestState.Preview && manager.RemainingTime == 0f, "normal quest previews before timed play");
            float preview = manager.PreviewRemainingTime;
            game.PauseGame();
            yield return new WaitForSecondsRealtime(0.12f);
            Require(manager.PreviewRemainingTime == preview, "pause freezes preview");
            game.ResumeGame();
            Invoke(manager, "StartPreviewedQuest");
            QuestRewardSO snapshot = manager.CurrentReward;
            int rewardAmount = snapshot.amount;
            Invoke(manager, "HandlePlayerSpawned", player);
            Require(manager.CurrentReward == snapshot && manager.CurrentProgress == 0, "same player rebind preserves snapshot");
            int beforeExp = level.currentExp;
            int successBefore = successes;
            typeof(QuestManager).GetProperty("RemainingTime").SetValue(manager, 0f);
            Invoke(manager, "Update");
            for (int i = 0; i < normal.targetAmount; i++) Invoke(manager, "HandleEnemyKilled", enemy);
            Invoke(manager, "LateUpdate");
            Invoke(manager, "SucceedCurrentQuest");
            Invoke(manager, "FailCurrentQuest");
            Require(successes == successBefore + 1 && level.currentExp == beforeExp + rewardAmount, "normal reward paid exactly once");
            Invoke(manager, "StartNormalQuestPreview");
            Invoke(manager, "StartPreviewedQuest");
            int stacks = player.GetComponent<PlayerAugments>().GetSkillStackCount(definition);
            int failBefore = failures;
            Invoke(manager, "FailCurrentQuest");
            Invoke(manager, "FailCurrentQuest");
            Require(failures == failBefore + 1 && !lastPenalty.HasPenalty &&
                player.GetComponent<PlayerAugments>().GetSkillStackCount(definition) == stacks, "normal timeout never removes power and resolves once");

            var skillGoal = AssetDatabase.LoadAssetAtPath<QuestSO>("Assets/Prefabs/Quest/Data/SkillHits_01.asset");
            Set(manager, "quests", new List<QuestSO> { skillGoal });
            Invoke(manager, "StartNormalQuestPreview");
            Invoke(manager, "StartPreviewedQuest");
            dealer.DealDamage(enemy, 0, PlayerDamageType.Skill);
            dealer.DealDamage(enemy, 1, PlayerDamageType.BasicAttack);
            Require(manager.CurrentProgress == 0, "zero damage and basic attacks do not count as skill hits");
            successBefore = successes;
            for (int i = 0; i < 5; i++) dealer.DealDamage(enemy, 1, PlayerDamageType.Skill, sourceSkill: skills.SkillQ);
            Require(successes == successBefore + 1 && manager.State == QuestState.Countdown,
                "five actual damaging skill ticks complete skill objective");
            while (game.IsHitStopped) yield return null;
            Set(manager, "quests", new List<QuestSO> { normal });

            yield return AugmentManager.Instance.ApplyAugmentRoutine(definition, game.SessionId, 0, (status, _) => applied = status);
            Require(player.GetComponent<PlayerAugments>().GetSkillStackCount(definition) >= 2, "risk fixture has valid stacked augment");
            Invoke(manager, "CreateChallengeZone");
            Require(manager.State == QuestState.ChallengePreview && manager.ChallengeZone != null, "voluntary zone is offered");
            var zone = manager.ChallengeZone;
            Require(manager.CurrentRiskPenaltyDescription.Contains(definition.augmentName),
                "risk notice names the owned augment definition");
            Vector3 originalPosition = player.transform.position;
            player.transform.position = zone.transform.position + Vector3.up * 5f;
            yield return null;
            Require(!zone.IsPlayerInWarningArea, "another floor cannot enter the challenge zone");
            player.transform.position = zone.transform.position;
            var presenter = Object.FindFirstObjectByType<QuestPanelUI>();
            presenter.enabled = false;
            yield return new WaitForSeconds(0.2f);
            Require(zone.DwellElapsed == 0f && manager.State == QuestState.ChallengePreview,
                "hidden risk warning cannot accept a challenge");
            presenter.enabled = true;
            yield return null;
            zone.SetNoticeVisible(true);
            yield return new WaitForSeconds(0.2f);
            player.transform.position = originalPosition + Vector3.right * 20f;
            yield return null;
            Require(manager.State == QuestState.ChallengePreview && zone.DwellElapsed == 0f, "passing through cannot accept challenge");
            stacks = player.GetComponent<PlayerAugments>().GetSkillStackCount(definition);
            failBefore = failures;
            Set(zone, "lifetime", 0.001f);
            yield return new WaitForSeconds(0.05f);
            Require(manager.ChallengeZone == null && failures == failBefore &&
                player.GetComponent<PlayerAugments>().GetSkillStackCount(definition) == stacks, "ignored zone expires without failure or loss");
            var originalRisk = (QuestSO)Get(manager, "riskChallengeQuest");
            var invalidSpawnRisk = Object.Instantiate(originalRisk);
            try
            {
                Set(manager, "riskChallengeQuest", invalidSpawnRisk);
                Invoke(manager, "CreateChallengeZone");
                var invalidZone = manager.ChallengeZone;
                Require(invalidZone != null, "spawn-failure fixture offered a valid preview");
                invalidSpawnRisk.eventMonsterPrefab = null;
                Invoke(manager, "HandleChallengeStartRequested", invalidZone);
                Require(manager.ChallengeZone == null && manager.State == QuestState.Countdown && failures == failBefore &&
                    player.GetComponent<PlayerAugments>().GetSkillStackCount(definition) == stacks,
                    "target spawn failure cancels without loss or failure event");
            }
            finally
            {
                Set(manager, "riskChallengeQuest", originalRisk);
                Object.Destroy(invalidSpawnRisk);
            }
            player.transform.position = originalPosition;
            Invoke(manager, "CreateChallengeZone");
            zone = manager.ChallengeZone;
            Require(zone != null, "risk zone can be offered again");
            player.transform.position = zone.transform.position;
            yield return new WaitForSeconds(1.7f);
            Require(manager.State == QuestState.Active && manager.CurrentQuest.category == QuestCategory.RiskChallenge,
                "visible notice plus 1.5-second dwell starts challenge");
            Invoke(manager, "FailCurrentQuest");
            Invoke(manager, "FailCurrentQuest");
            Require(player.GetComponent<PlayerAugments>().GetSkillStackCount(definition) == stacks - 1 && failures == failBefore + 1,
                "risk failure reduces only declared stack once");
            player.transform.position = originalPosition;
            yield return AugmentManager.Instance.ApplyAugmentRoutine(definition, game.SessionId, 0, (status, _) => applied = status);
            Invoke(manager, "CreateChallengeZone");
            zone = manager.ChallengeZone;
            player.transform.position = zone.transform.position;
            yield return new WaitForSeconds(1.7f);
            Require(manager.State == QuestState.Active, "second challenge starts");
            int tickets = UIManager.Instance.DeferredAugmentTickets;
            KillWithoutExperience((EnemyHealth)Get(manager, "activeEventMonster"));
            Require(UIManager.Instance.DeferredAugmentTickets == tickets + 1 && !game.IsPaused,
                "risk success stores choice without pausing combat");
            int pendingBefore = (int)Get(UIManager.Instance, "pendingTickets");
            UIManager.Instance.RequestAugmentChoice();
            Require(UIManager.Instance.DeferredAugmentTickets == 0 && game.IsPaused &&
                (int)Get(UIManager.Instance, "pendingTickets") == pendingBefore + tickets + 2,
                "next level-up choice consumes the deferred ticket queue");
            UIManager.Instance.ClearSession(true);
            int successCount = successes, failureCount = failures;
            Invoke(manager, "StartNormalQuestPreview");
            Invoke(manager, "StartPreviewedQuest");
            var playerHealth = player.GetComponent<PlayerHealth>();
            typeof(PlayerHealth).GetProperty("IsDead").SetValue(playerHealth, true);
            Invoke(manager, "Update");
            typeof(PlayerHealth).GetProperty("IsDead").SetValue(playerHealth, false);
            Invoke(manager, "SucceedCurrentQuest");
            Invoke(manager, "FailCurrentQuest");
            Require(manager.State == QuestState.Stopped && manager.CurrentQuest == null && manager.ChallengeZone == null &&
                successes == successCount && failures == failureCount, "session cancellation cannot pay or penalize");
            UIManager.Instance.ClearSession(false);
            Require(UIManager.Instance.DeferredAugmentTickets == 0, "session cleanup discards deferred choices");
            player.transform.position = originalPosition;
            Debug.Log("QUEST_FLOW_VALIDATION_PASS");
        }
        finally
        {
            manager.onQuestSucceeded -= success;
            manager.onQuestFailed -= failure;
            manager.ClearSession();
            Set(manager, "quests", normalDefinitions);
            if (hadHistory) PlayerPrefs.SetInt(key, history); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            if (game.IsPaused) game.ResumeGame();
            Object.Destroy(target);
        }
    }
    private static void KillWithoutExperience(EnemyHealth enemy)
    {
        Set(enemy, "stats", null);
        Set(enemy, "expReward", 0);
        enemy.TakeDamage(int.MaxValue);
    }
    private static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("QUEST_FLOW_FAIL: " + message);
        Debug.Log("QUEST_FLOW_PASS: " + message);
    }
}
