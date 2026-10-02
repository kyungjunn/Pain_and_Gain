using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CharacterHudBuilder
{
    private const string PortraitFolder = "Assets/Images/CharacterPortraits";
    private static readonly Color Background = new Color(0.045f, 0.055f, 0.075f, 0.98f);
    private static readonly Color Surface = new Color(0.085f, 0.10f, 0.13f, 1f);
    private static readonly Color Gold = new Color(0.76f, 0.64f, 0.40f, 1f);
    private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/양진체v0.93 SDF.asset");

    [MenuItem("Tools/UI/Build Character HUD")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Build the character HUD outside Play Mode.");
        if (Font == null) throw new InvalidOperationException("Korean UI font is missing.");
        Directory.CreateDirectory(PortraitFolder);
        AssetDatabase.Refresh();
        foreach (string name in new[] { "Sword", "Wizard", "Ninja" }) CapturePortrait(name);
        BuildCharacterPanel();
        BuildPlayerBar();
        BuildCanvas();
        Validate();
    }

    private static void CapturePortrait(string name)
    {
        var preview = new PreviewRenderUtility();
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Player/{name}Player.prefab");
            var actor = UnityEngine.Object.Instantiate(prefab);
            preview.AddSingleGO(actor);
            actor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            actor.transform.localScale = Vector3.one;
            foreach (var script in actor.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            foreach (var canvas in actor.GetComponentsInChildren<Canvas>(true)) canvas.gameObject.SetActive(false);
            foreach (var particles in actor.GetComponentsInChildren<ParticleSystem>(true)) particles.gameObject.SetActive(false);
            var animator = actor.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
                throw new InvalidOperationException(name + " has no Animator controller.");
            var idle = animator.runtimeAnimatorController.animationClips
                .FirstOrDefault(c => c.name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0);
            if (idle == null) throw new InvalidOperationException(name + " has no Idle clip.");
            animator.enabled = false;
            idle.SampleAnimation(animator.gameObject, Mathf.Min(0.35f, idle.length * 0.5f));
            var renderers = actor.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException(name + " has no visible model.");
            var bounds = VisibleBounds(renderers[0]);
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(VisibleBounds(renderer));
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x * 1.5f) * 1.13f;
            preview.camera.nearClipPlane = 0.01f;
            preview.camera.farClipPlane = 50f;
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = Color.clear;
            preview.camera.transform.position = bounds.center + Vector3.forward * 6f;
            preview.camera.transform.LookAt(bounds.center);
            preview.ambientColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            preview.lights[0].intensity = 1.3f;
            preview.lights[0].transform.rotation = Quaternion.Euler(30f, 200f, 0f);
            preview.lights[1].intensity = 0.8f;
            preview.lights[1].transform.rotation = Quaternion.Euler(340f, 130f, 0f);
            preview.BeginPreview(new Rect(0, 0, 512, 768), GUIStyle.none);
            preview.Render(true);
            var rendered = (RenderTexture)preview.EndPreview();
            var previous = RenderTexture.active;
            var image = new Texture2D(rendered.width, rendered.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = rendered;
                image.ReadPixels(new Rect(0, 0, rendered.width, rendered.height), 0, 0);
                if (QualitySettings.activeColorSpace == ColorSpace.Linear && !rendered.sRGB)
                {
                    var pixels = image.GetPixels();
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = pixels[i].gamma;
                    image.SetPixels(pixels);
                }
                image.Apply();
            }
            finally { RenderTexture.active = previous; }
            string path = $"{PortraitFolder}/{name}Idle.png";
            var cropped = CropPortrait(image);
            File.WriteAllBytes(path, cropped.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(cropped);
            UnityEngine.Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            Debug.Log($"Character portrait: {name}, Idle clip: {idle.name}");
        }
        finally { preview.Cleanup(); }
    }

    private static Texture2D CropPortrait(Texture2D source)
    {
        var pixels = source.GetPixels32();
        int left = source.width, right = -1, bottom = source.height, top = -1;
        for (int y = 0; y < source.height; y++)
            for (int x = 0; x < source.width; x++)
            {
                if (pixels[y * source.width + x].a == 0) continue;
                left = Mathf.Min(left, x);
                right = Mathf.Max(right, x);
                bottom = Mathf.Min(bottom, y);
                top = Mathf.Max(top, y);
            }
        if (right < left) throw new InvalidOperationException("Portrait render is empty.");
        int width = right - left + 1, height = top - bottom + 1;
        int padding = Mathf.CeilToInt(Mathf.Max(width, height) * 0.05f);
        var result = new Texture2D(width + padding * 2, height + padding * 2, TextureFormat.RGBA32, false);
        var cropped = new Color32[result.width * result.height];
        for (int y = 0; y < height; y++)
            Array.Copy(pixels, (bottom + y) * source.width + left, cropped,
                (y + padding) * result.width + padding, width);
        result.SetPixels32(cropped);
        result.Apply();
        return result;
    }

    private static Bounds VisibleBounds(Renderer renderer)
    {
        if (!(renderer is SkinnedMeshRenderer skinned)) return renderer.bounds;
        var mesh = new Mesh();
        try
        {
            skinned.BakeMesh(mesh);
            var vertices = mesh.vertices;
            if (vertices.Length == 0) return renderer.bounds;
            var bounds = new Bounds(skinned.transform.TransformPoint(vertices[0]), Vector3.zero);
            foreach (var vertex in vertices) bounds.Encapsulate(skinned.transform.TransformPoint(vertex));
            return bounds;
        }
        finally { UnityEngine.Object.DestroyImmediate(mesh); }
    }

    private static void BuildCharacterPanel()
    {
        const string path = "Assets/Prefabs/UI/StatsPanel.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
            foreach (var binding in root.GetComponents<StatsPanelUI>()) UnityEngine.Object.DestroyImmediate(binding);
            foreach (var binding in root.GetComponents<CharacterPortraitUI>()) UnityEngine.Object.DestroyImmediate(binding);
            root.name = "CharacterInfoPanel";
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.05f, 0.15f);
            rect.anchorMax = new Vector2(0.95f, 0.9f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = Background;
            root.GetComponent<Image>().raycastTarget = true;
            var title = Text(root.transform, "Title", "캐릭터 정보", 26, Gold);
            Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -54), new Vector2(-24, -8));
            var hint = Text(root.transform, "CloseHint", "TAB  닫기", 15, Color.gray);
            hint.alignment = TextAlignmentOptions.Right;
            Place(hint.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -54), new Vector2(-24, -8));
            var rule = Panel(root.transform, "HeaderLine", Gold);
            Place(rule, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -61), new Vector2(-24, -59));

            var stats = Panel(root.transform, "StatsPanel", Surface);
            Place(stats, Vector2.zero, new Vector2(0.29f, 1), new Vector2(20, 20), new Vector2(-8, -76));
            Header(stats, "능력치");
            var bindingUI = stats.gameObject.AddComponent<StatsPanelUI>();
            bindingUI.damageText = StatRow(stats, "AttackDamage", "공격력", 0);
            bindingUI.defenseText = StatRow(stats, "Defense", "방어력", 1);
            bindingUI.attackSpeedText = StatRow(stats, "AttackSpeed", "공격속도", 2);
            bindingUI.moveSpeedText = StatRow(stats, "MoveSpeed", "이동속도", 3);

            var portraitPanel = Panel(root.transform, "PortraitPanel", new Color(0.10f, 0.12f, 0.16f, 1));
            Place(portraitPanel, new Vector2(0.29f, 0), new Vector2(0.68f, 1), new Vector2(4, 20), new Vector2(-4, -76));
            Header(portraitPanel, "캐릭터");
            var portraitRect = Panel(portraitPanel, "Portrait", Color.white);
            Place(portraitRect, Vector2.zero, Vector2.one, new Vector2(16, 12), new Vector2(-16, -56));
            var portrait = portraitRect.GetComponent<Image>();
            portrait.preserveAspect = true;
            var portraitBinding = root.AddComponent<CharacterPortraitUI>();
            SetObject(portraitBinding, "portrait", portrait);
            foreach (string name in new[] { "Sword", "Wizard", "Ninja" })
                SetObject(portraitBinding, name.ToLowerInvariant() + "Portrait", AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitFolder}/{name}Idle.png"));
            portrait.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitFolder}/SwordIdle.png");

            var inventory = Panel(root.transform, "InventoryPanel", Surface);
            Place(inventory, new Vector2(0.68f, 0), Vector2.one, new Vector2(8, 20), new Vector2(-20, -76));
            Header(inventory, "인벤토리");
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 3; col++)
                {
                    var slot = Panel(inventory, $"Slot_{row}_{col}", new Color(0.23f, 0.25f, 0.29f, 1));
                    float left = 0.07f + col * 0.29f;
                    float top = 0.78f - row * 0.19f;
                    Place(slot, new Vector2(left, top - 0.16f), new Vector2(left + 0.25f, top), Vector2.zero, Vector2.zero);
                    var inner = Panel(slot, "Empty", Background);
                    Place(inner, Vector2.zero, Vector2.one, Vector2.one * 2, -Vector2.one * 2);
                }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void BuildPlayerBar()
    {
        const string path = "Assets/Prefabs/Player/BasePlayer.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var existing = root.transform.Find("PlayerHealthCanvas");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var canvasObject = new GameObject("PlayerHealthCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.layer = 5;
            canvasObject.transform.SetParent(root.transform, false);
            canvasObject.transform.localPosition = new Vector3(0, 0.1f, 0);
            canvasObject.transform.localScale = Vector3.one * 0.009f;
            canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 12);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 5;
            var slider = HealthSlider(canvasObject.transform, "HealthBar");
            var sliderRect = slider.GetComponent<RectTransform>();
            sliderRect.anchoredPosition = new Vector2(0, -18);
            sliderRect.sizeDelta = new Vector2(100, 10);
            var binding = canvasObject.AddComponent<PlayerHealthBarUI>();
            SetObject(binding, "slider", slider);
            SetBool(binding, "faceCamera", true);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void BuildCanvas()
    {
        const string path = "Assets/Prefabs/UI/MainCanvas.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var info = root.GetComponentInChildren<CharacterPortraitUI>(true).gameObject;
            info.name = "CharacterInfoPanel";
            var infoRect = info.GetComponent<RectTransform>();
            Place(infoRect, new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.9f), Vector2.zero, Vector2.zero);
            infoRect.SetAsLastSibling();
            info.SetActive(false);
            SetObject(root.GetComponent<CombatHudUI>(), "statsPanel", info);
            var existing = root.transform.Find("PlayerHealthHUD");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var exp = root.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "LevelExpBar");
            var slider = HealthSlider(root.transform, "PlayerHealthHUD");
            var rect = slider.GetComponent<RectTransform>();
            rect.anchorMin = exp.anchorMin;
            rect.anchorMax = exp.anchorMax;
            rect.pivot = exp.pivot;
            rect.anchoredPosition = exp.anchoredPosition + new Vector2(0, 64);
            rect.sizeDelta = new Vector2(exp.sizeDelta.x, 24);
            var value = Text(rect, "HealthValue", "", 17, Color.white);
            Stretch(value.rectTransform);
            value.alignment = TextAlignmentOptions.Center;
            var binding = slider.gameObject.AddComponent<PlayerHealthBarUI>();
            SetObject(binding, "slider", slider);
            SetObject(binding, "valueText", value);
            SetBool(binding, "bindSpawnedPlayer", true);
            infoRect.SetAsLastSibling();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Slider HealthSlider(Transform parent, string name)
    {
        var rect = Panel(parent, name, new Color(0.03f, 0.03f, 0.04f, 1));
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        var track = Panel(rect, "Track", new Color(0.22f, 0.07f, 0.08f, 1));
        Place(track, Vector2.zero, Vector2.one, Vector2.one * 2, -Vector2.one * 2);
        var fill = Panel(track, "Fill", new Color(0.85f, 0.12f, 0.16f, 1));
        Stretch(fill);
        var slider = rect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0;
        slider.maxValue = 1;
        slider.value = 1;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        return slider;
    }

    private static TextMeshProUGUI StatRow(Transform parent, string name, string label, int index)
    {
        var text = Text(parent, name, label, 19, new Color(0.88f, 0.90f, 0.94f));
        Place(text.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(18, -110 - index * 55), new Vector2(-14, -66 - index * 55));
        return text;
    }

    private static void Header(Transform parent, string label)
    {
        var text = Text(parent, "Heading", label, 21, Gold);
        Place(text.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(18, -50), new Vector2(-18, -10));
    }

    private static TextMeshProUGUI Text(Transform parent, string name, string value, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = Font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform Panel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        go.GetComponent<Image>().raycastTarget = false;
        return go.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect) => Place(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    private static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
    private static void SetObject(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void SetBool(UnityEngine.Object target, string field, bool value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void Validate()
    {
        foreach (string name in new[] { "Sword", "Wizard", "Ninja" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Player/{name}Player.prefab");
            if (prefab.GetComponentsInChildren<PlayerHealthBarUI>(true).Length != 1)
                throw new InvalidOperationException(name + " must inherit one health bar.");
            if (AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitFolder}/{name}Idle.png") == null)
                throw new InvalidOperationException(name + " portrait missing.");
        }
        var canvas = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/MainCanvas.prefab");
        if (canvas.GetComponentsInChildren<PlayerHealthBarUI>(true).Length != 1 ||
            canvas.GetComponentsInChildren<StatsPanelUI>(true).Length != 1 ||
            canvas.GetComponentsInChildren<CharacterPortraitUI>(true).Length != 1)
            throw new InvalidOperationException("MainCanvas bindings are incomplete.");
        var info = canvas.GetComponentInChildren<CharacterPortraitUI>(true);
        if (info.gameObject.activeSelf) throw new InvalidOperationException("Character window must start closed.");
        Debug.Log("Character HUD prefab validation passed for all three characters.");
    }
}
