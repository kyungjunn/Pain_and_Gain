using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One runtime tooltip owned by the root canvas, outside scaled inventory content.
public sealed class AugmentTooltipUI : MonoBehaviour
{
    private Canvas canvas;
    private RectTransform canvasRect;
    private RectTransform panel;
    private TextMeshProUGUI text;
    private AugmentInventorySlotUI hovered;
    private AugmentInventorySlotUI focused;
    private AugmentInventorySlotUI displayed;
    private readonly Vector3[] corners = new Vector3[4];

    internal static AugmentTooltipUI ForCanvas(Canvas canvas)
    {
        var existing = canvas.GetComponentInChildren<AugmentTooltipUI>(true);
        if (existing != null) return existing;

        var host = new GameObject("AugmentTooltip", typeof(RectTransform), typeof(AugmentTooltipUI));
        host.layer = canvas.gameObject.layer;
        host.transform.SetParent(canvas.transform, false);
        var tooltip = host.GetComponent<AugmentTooltipUI>();
        tooltip.canvas = canvas;
        tooltip.canvasRect = (RectTransform)canvas.transform;
        tooltip.panel = (RectTransform)host.transform;
        tooltip.panel.anchorMin = tooltip.panel.anchorMax = new Vector2(0.5f, 0.5f);
        tooltip.panel.pivot = Vector2.zero;
        var background = host.AddComponent<Image>();
        background.color = new Color(0.06f, 0.07f, 0.1f, 0.97f);
        background.raycastTarget = false;
        var group = host.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        var label = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.layer = host.layer;
        label.transform.SetParent(host.transform, false);
        tooltip.text = label.GetComponent<TextMeshProUGUI>();
        var rect = tooltip.text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(16f, 16f);
        rect.offsetMax = new Vector2(-16f, -16f);
        tooltip.text.raycastTarget = false;
        tooltip.text.richText = false;
        tooltip.text.color = Color.white;
        tooltip.text.textWrappingMode = TextWrappingModes.Normal;
        tooltip.text.enableAutoSizing = true;
        tooltip.text.fontSizeMin = 1f;
        host.SetActive(false);
        return tooltip;
    }

    internal void SetHovered(AugmentInventorySlotUI slot) { hovered = slot; Refresh(); }
    internal void SetFocused(AugmentInventorySlotUI slot) { focused = slot; Refresh(); }
    internal void ClearHovered(AugmentInventorySlotUI slot)
    {
        if (hovered == slot) { hovered = null; Refresh(); }
    }
    internal void ClearFocused(AugmentInventorySlotUI slot)
    {
        if (focused == slot) { focused = null; Refresh(); }
    }

    private static bool CanShow(AugmentInventorySlotUI slot)
    {
        return slot != null && slot.isActiveAndEnabled && slot.Augment != null;
    }

    private void Refresh()
    {
        var slot = CanShow(hovered) ? hovered : CanShow(focused) ? focused : null;
        displayed = slot;
        gameObject.SetActive(slot != null);
        if (slot == null) return;
        transform.SetAsLastSibling();
        if (slot.NameText != null)
        {
            text.font = slot.NameText.font;
            text.fontSharedMaterial = slot.NameText.fontSharedMaterial;
            // The source font is already expressed in the 1920x1080 canvas units.
            text.fontSizeMax = Mathf.Clamp(slot.NameText.fontSize, 22f, 36f);
        }
        else text.fontSizeMax = 26f;
        text.fontSize = text.fontSizeMax;
        var augment = slot.Augment;
        string maximum = augment.maxStacks == 0 ? "무제한" : augment.maxStacks.ToString();
        text.text = $"{augment.augmentName}\n\n{augment.description}\n\n중첩: {slot.Stacks} / {maximum}";
        Position();
    }

    private void LateUpdate()
    {
        if (!CanShow(displayed)) Refresh();
        else Position();
    }

    private void Position()
    {
        Rect bounds = canvasRect.rect;
        Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, Vector2.zero, camera, out var screenMin) &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, new Vector2(Screen.width, Screen.height), camera, out var screenMax))
        {
            bounds = Rect.MinMaxRect(Mathf.Max(bounds.xMin, screenMin.x), Mathf.Max(bounds.yMin, screenMin.y),
                Mathf.Min(bounds.xMax, screenMax.x), Mathf.Min(bounds.yMax, screenMax.y));
        }
        const float margin = 8f;
        float width = Mathf.Max(1f, Mathf.Min(440f, bounds.width - margin * 2f));
        // Measure at the intended font size before fitting unusually long descriptions.
        text.enableAutoSizing = false;
        text.fontSize = text.fontSizeMax;
        float height = text.GetPreferredValues(text.text, Mathf.Max(1f, width - 32f), Mathf.Infinity).y + 32f;
        text.enableAutoSizing = true;
        height = Mathf.Max(1f, Mathf.Min(height, bounds.height - margin * 2f));
        panel.sizeDelta = new Vector2(width, height);
        ((RectTransform)displayed.transform).GetWorldCorners(corners);
        Vector3 anchor = canvasRect.InverseTransformPoint(corners[2]);
        float x = anchor.x + margin;
        if (x + width > bounds.xMax - margin) x = canvasRect.InverseTransformPoint(corners[1]).x - width - margin;
        x = Mathf.Clamp(x, bounds.xMin + margin, Mathf.Max(bounds.xMin + margin, bounds.xMax - width - margin));
        float y = Mathf.Clamp(anchor.y - height, bounds.yMin + margin,
            Mathf.Max(bounds.yMin + margin, bounds.yMax - height - margin));
        panel.localPosition = new Vector3(x, y, 0f);
    }
}
