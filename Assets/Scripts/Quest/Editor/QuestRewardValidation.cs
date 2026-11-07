using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class QuestRewardValidation
{
    [MenuItem("Tools/Quest/Validate Rewards")]
    public static void Run()
    {
        var player = new GameObject("QuestRewardTestPlayer");
        player.SetActive(false);
        var pool = ScriptableObject.CreateInstance<QuestRewardPoolSO>();
        var experience = ScriptableObject.CreateInstance<ExperienceRewardSO>();
        var invalid = ScriptableObject.CreateInstance<ExperienceRewardSO>();
        var heal = ScriptableObject.CreateInstance<HealRewardSO>();
        var charge = ScriptableObject.CreateInstance<UltimateChargeRewardSO>();
        var augment = ScriptableObject.CreateInstance<AugmentChoiceRewardSO>();
        experience.amount = 25;
        invalid.amount = 0;
        heal.amount = 20;
        charge.amount = 25;
        try
        {
            QuestRewardSO selected;
            Require(!pool.TrySelect(0, out selected), "empty pool rejected");
            pool.entries.Add(Entry(experience, 1));
            pool.entries.Add(Entry(heal, 3));
            Require(pool.TrySelect(0, out selected) && selected == experience, "lower boundary");
            Require(pool.TrySelect(0.249f, out selected) && selected == experience, "first interval");
            Require(pool.TrySelect(0.25f, out selected) && selected == heal, "weight boundary");
            Require(pool.TrySelect(1, out selected) && selected == heal, "upper boundary");
            Require(!pool.TrySelect(float.NaN, out selected) && !pool.TrySelect(-1, out selected) &&
                !pool.TrySelect(1.01f, out selected), "invalid samples rejected");
            pool.entries.Clear();
            pool.entries.Add(Entry(null, 1));
            pool.entries.Add(Entry(invalid, 1));
            pool.entries.Add(Entry(heal, 0));
            Require(!pool.TrySelect(0, out selected), "invalid entries rejected");
            pool.entries.Add(Entry(experience, 1));
            Require(pool.TrySelect(1, out selected) && selected == experience, "single eligible reward");
            Require(!pool.TrySelect(0, _ => false, out selected), "eligibility filter respected");
            Require(!experience.Apply(player).applied, "missing component rejected");
            var level = player.AddComponent<PlayerLevelSystem>();
            Require(experience.Apply(player).applied && level.currentExp == 25, "experience granted");
            Require(!invalid.Apply(player).applied && !experience.Apply(null).applied, "invalid reward rejected");
            var health = player.AddComponent<PlayerHealth>();
            typeof(PlayerHealth).GetProperty("CurrentHealth").SetValue(health, health.MaxHealth);
            Require(heal.Apply(player).grantedAmount == 0, "full health gives no substitute");
            typeof(PlayerHealth).GetProperty("CurrentHealth").SetValue(health, health.MaxHealth - 5);
            var healing = heal.Apply(player);
            Require(healing.grantedAmount == 5 && health.CurrentHealth == health.MaxHealth &&
                healing.Description == "체력 5 회복", "healing clamps and reports actual amount");
            var gauge = player.AddComponent<PlayerUltimateGauge>();
            gauge.Add(95);
            Require(charge.Apply(player).grantedAmount == 5 && gauge.IsFull, "charge clamps");
            Require(!augment.Apply(player).applied, "missing active player session rejects choice");
            typeof(PlayerHealth).GetProperty("IsDead").SetValue(health, true);
            Require(!experience.Apply(player).applied, "dead player cannot receive reward");
            Require(new QuestRewardResult { applied = true }.Description == "추가 보상 없음", "no-extra-reward tutorial is not an error");
            Require(experience.DescribeGranted(0) == "경험치 0 획득", "zero tutorial XP reports actual grant");
            var standard = AssetDatabase.LoadAssetAtPath<QuestRewardPoolSO>("Assets/Prefabs/Quest/Data/StandardRewards.asset");
            Require(standard != null && standard.entries.Count == 2 && standard.entries[0].reward is ExperienceRewardSO &&
                standard.entries[1].reward is HealRewardSO, "normal hunt offers only experience or healing");
            var risk = AssetDatabase.LoadAssetAtPath<QuestRewardPoolSO>("Assets/Prefabs/Quest/Data/RiskChallengeRewards.asset");
            Require(risk != null && risk.entries.Count == 1 && risk.entries[0].reward is AugmentChoiceRewardSO,
                "risk reward is a deferred augment choice");
            Debug.Log("QUEST_REWARD_VALIDATION_PASS");
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(pool);
            Object.DestroyImmediate(experience);
            Object.DestroyImmediate(invalid);
            Object.DestroyImmediate(heal);
            Object.DestroyImmediate(charge);
            Object.DestroyImmediate(augment);
        }
    }
    private static QuestRewardEntry Entry(QuestRewardSO reward, int weight) => new QuestRewardEntry { reward = reward, weight = weight };
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("QUEST_REWARD_FAIL: " + message);
        Debug.Log("QUEST_REWARD_PASS: " + message);
    }
}
