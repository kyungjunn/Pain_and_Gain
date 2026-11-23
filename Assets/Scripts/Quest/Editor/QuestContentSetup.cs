using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

public static class QuestContentSetup
{
    private const string Data = "Assets/Prefabs/Quest/Data/";

    [MenuItem("Tools/Quest/Author Designed Quest Content")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Author quest content in Edit Mode.");
        var experience = Load<ExperienceRewardSO>(Data + "Experience25.asset");
        var heal = Load<HealRewardSO>(Data + "Heal20.asset");
        var choice = Load<AugmentChoiceRewardSO>(Data + "AugmentChoice1.asset");
        var rabbit = Load<GameObject>("Assets/Prefabs/Enemy/LevelOne/RabbitEnemy.prefab");
        var giant = Load<GameObject>("Assets/Prefabs/Enemy/GiantEnemy.prefab");
        var beacon = Load<GameObject>("Assets/Prefabs/Quest/RabbitBeacon.prefab");
        var xpPool = Pool("NormalKillRewards", experience);
        var healPool = Pool("NormalSkillRewards", heal);
        var standardPool = Pool("StandardRewards", experience, heal);
        var riskPool = Pool("RiskChallengeRewards", choice);

        var tutorial = new[]
        {
            Quest("Tutorial_Kills", "길잡이 — 첫 전투", "적 3마리를 처치하세요.", "Tutorial", "KillEnemies", 3, 0f, experience, null, null, null),
            Quest("Tutorial_Q", "길잡이 — Q 스킬", "장착된 Q 스킬로 적에게 실제 피해를 주세요.", "Tutorial", "QSkillHits", 1, 0f, heal, null, null, null),
            Quest("Tutorial_Augment", "길잡이 — 첫 성장", "첫 증강을 선택하세요. 앞서 선택했다면 바로 인정됩니다.", "Tutorial", "FirstAugment", 1, 0f, null, null, null, null),
            Quest("Tutorial_Hunt", "길잡이 — 목표 추적", "비콘으로 표시된 연습 토끼를 찾아 처치하세요.", "Tutorial", "HuntEventMonster", 1, 0f, experience, null, rabbit, beacon)
        };
        var normal = new[]
        {
            Quest("KillEnemies_01", "일반 — 적 처치", "45초 안에 일반 적 8마리를 처치하세요.", "Normal", "KillEnemies", 8, 45f, null, xpPool, null, null),
            Quest("SkillHits_01", "일반 — 스킬 활용", "장착 스킬로 실제 피해 적중 5회 · 다단 히트 포함", "Normal", "SkillHits", 5, 40f, null, healPool, null, null),
            Quest("HuntRabbit", "일반 — 토끼 추적", "45초 안에 표시된 토끼를 찾아 처치하세요.", "Normal", "HuntEventMonster", 1, 45f, null, standardPool, rabbit, beacon)
        };
        var risk = Quest("HuntGiant", "위험 도전 — 거인 사냥", "60초 안에 표시된 거인을 처치하세요.", "RiskChallenge", "HuntEventMonster", 1, 60f, null, riskPool, giant, beacon);
        GameObject zone = CreateZone();
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Manager/Managers.prefab");
        try
        {
            var manager = root.GetComponentInChildren<QuestManager>(true);
            if (manager == null) throw new InvalidOperationException("QuestManager missing on Managers prefab.");
            var so = new SerializedObject(manager);
            SetArray(so, "tutorialQuests", tutorial);
            SetArray(so, "quests", normal);
            Property(so, "riskChallengeQuest").objectReferenceValue = risk;
            var zoneProperty = Property(so, "challengeZonePrefab");
            zoneProperty.objectReferenceValue = zone;
            Property(so, "firstQuestDelay").floatValue = 15f;
            Property(so, "questCooldown").floatValue = 45f;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Manager/Managers.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        ConfigureQuestPanel();
        AssetDatabase.SaveAssets();
        Debug.Log("QUEST_CONTENT_SETUP_PASS: four tutorial stages, three normal objectives, voluntary giant challenge.");
    }

    private static void ConfigureQuestPanel()
    {
        const string path = "Assets/Prefabs/UI/QuestPanel.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var ui = root.GetComponent<QuestPanelUI>();
            if (ui == null || ui.descriptionText == null)
                throw new InvalidOperationException("Quest panel requires its existing presenter and font.");
            TMP_FontAsset font = ui.descriptionText.font;
            ((RectTransform)ui.questCardGroup.transform).sizeDelta = new Vector2(448f, 220f);
            ((RectTransform)ui.penaltyGroup.transform).anchoredPosition = new Vector2(0f, -254f);
            ui.descriptionText.rectTransform.sizeDelta = new Vector2(-132f, 100f);
            ui.descriptionText.fontSize = 18f;
            ui.descriptionText.textWrappingMode = TextWrappingModes.Normal;
            ui.questNameText.enableAutoSizing = true;
            ui.questNameText.fontSizeMin = 14f;
            ui.questNameText.fontSizeMax = 22f;
            ui.timerText.enableAutoSizing = true;
            ui.timerText.fontSizeMin = 12f;
            ui.timerText.fontSizeMax = 22f;
            ui.previewTimerText = ui.timerText;
            ui.timerFillImage.type = Image.Type.Filled;
            ui.timerFillImage.fillMethod = Image.FillMethod.Horizontal;
            ui.timerFillImage.fillOrigin = 0;
            var notice = Rect(root.transform, "RiskNotice", new Vector2(-20f, -68f), new Vector2(460f, 390f));
            ui.riskNoticeGroup = notice.GetComponent<CanvasGroup>();
            if (ui.riskNoticeGroup == null) ui.riskNoticeGroup = notice.gameObject.AddComponent<CanvasGroup>();
            ui.riskNoticeGroup.alpha = 0f;
            var background = notice.GetComponent<Image>();
            if (background == null) background = notice.gameObject.AddComponent<Image>();
            background.color = new Color(0.12f, 0.04f, 0.035f, 0.94f);
            background.raycastTarget = false;
            ui.riskNoticeText = Label(notice, "RiskDetails", font, new Vector2(-18f, -16f), new Vector2(424f, 320f), 19f);
            ui.riskNoticeText.text = string.Empty;
            ui.dwellProgressText = Label(notice, "DwellProgress", font, new Vector2(-18f, -348f), new Vector2(424f, 28f), 20f);
            ui.deferredTicketsText = Label(root.transform, "DeferredChoices", font, new Vector2(-20f, -20f), new Vector2(460f, 32f), 20f);
            ui.deferredTicketsText.text = string.Empty;
            ui.deferredTicketsText.enabled = false;
            ((RectTransform)ui.resultToastGroup.transform).sizeDelta = new Vector2(460f, 110f);
            ui.resultMessageText.rectTransform.sizeDelta = new Vector2(-24f, 64f);
            ui.resultMessageText.fontSize = 16f;
            ui.resultMessageText.textWrappingMode = TextWrappingModes.Normal;
            EditorUtility.SetDirty(ui);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var existing = parent.Find(name);
        var rect = existing != null ? (RectTransform)existing : (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.gameObject.layer = 5;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, TMP_FontAsset font,
        Vector2 position, Vector2 size, float fontSize)
    {
        var rect = Rect(parent, name, position, size);
        var text = rect.GetComponent<TextMeshProUGUI>();
        if (text == null) text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static QuestSO Quest(string assetName, string title, string description, string category,
        string objective, int target, float time, QuestRewardSO reward, QuestRewardPoolSO pool,
        GameObject monster, GameObject beacon)
    {
        string path = Data + assetName + ".asset";
        var quest = AssetDatabase.LoadAssetAtPath<QuestSO>(path);
        if (quest == null)
        {
            quest = ScriptableObject.CreateInstance<QuestSO>();
            AssetDatabase.CreateAsset(quest, path);
        }
        var so = new SerializedObject(quest);
        Property(so, "questName").stringValue = title;
        Property(so, "description").stringValue = description;
        SetEnum(so, "category", category);
        SetEnum(so, "objectiveType", objective);
        Property(so, "targetAmount").intValue = target;
        Property(so, "timeLimit").floatValue = time;
        Property(so, "successReward").objectReferenceValue = reward;
        Property(so, "rewardPool").objectReferenceValue = pool;
        Property(so, "eventMonsterPrefab").objectReferenceValue = monster;
        Property(so, "eventMonsterBeaconPrefab").objectReferenceValue = beacon;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(quest);
        return quest;
    }

    private static QuestRewardPoolSO Pool(string name, params QuestRewardSO[] rewards)
    {
        string path = Data + name + ".asset";
        var pool = AssetDatabase.LoadAssetAtPath<QuestRewardPoolSO>(path);
        if (pool == null)
        {
            pool = ScriptableObject.CreateInstance<QuestRewardPoolSO>();
            AssetDatabase.CreateAsset(pool, path);
        }
        pool.entries.Clear();
        foreach (var reward in rewards)
            pool.entries.Add(new QuestRewardEntry { reward = reward, weight = 1 });
        EditorUtility.SetDirty(pool);
        return pool;
    }

    private static GameObject CreateZone()
    {
        var root = new GameObject("QuestChallengeZone");
        try
        {
            var zone = root.AddComponent<QuestChallengeZone>();
            var warning = Ring(root.transform, "WarningArea", new Color(1f, 0.6f, 0.1f, 0.85f));
            var start = Ring(root.transform, "StartArea", new Color(1f, 0.15f, 0.08f, 1f));
            var progress = Ring(root.transform, "DwellProgress", Color.white);
            progress.GetComponent<LineRenderer>().loop = false;
            var so = new SerializedObject(zone);
            Property(so, "warningAreaVisual").objectReferenceValue = warning;
            Property(so, "startAreaVisual").objectReferenceValue = start;
            Property(so, "dwellProgressVisual").objectReferenceValue = progress.GetComponent<LineRenderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Quest/QuestChallengeZone.prefab");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static GameObject Ring(Transform parent, string name, Color color)
    {
        string materialPath = "Assets/Prefabs/Quest/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Load<Material>("Assets/Prefabs/Quest/RabbitBeacon.mat"));
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.SetColor("_Tint", color);
        EditorUtility.SetDirty(material);
        var go = new GameObject(name, typeof(LineRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.up * 0.08f;
        var line = go.GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.sharedMaterial = material;
        line.startWidth = line.endWidth = 0.1f;
        line.positionCount = 96;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / line.positionCount;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.5f);
        }
        return go;
    }

    private static T Load<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing asset: " + path);
    private static SerializedProperty Property(SerializedObject so, string name) =>
        so.FindProperty(name) ?? throw new InvalidOperationException("Missing field: " + name);
    private static void SetEnum(SerializedObject so, string field, string name)
    {
        var property = Property(so, field);
        int index = Array.IndexOf(property.enumNames, name);
        if (index < 0) throw new InvalidOperationException("Unknown enum value: " + name);
        property.enumValueIndex = index;
    }
    private static void SetArray(SerializedObject so, string field, QuestSO[] values)
    {
        var property = Property(so, field);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
