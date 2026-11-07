using UnityEngine;
using UnityEngine.UI;

// 현재 체력은 건드리지 않고, 최근에 잃은 구간만 잠깐 흰색으로 표시한다.
public sealed class HealthDamageTrail : MonoBehaviour
{
    private const float HoldDuration = 0.06f;
    private const float CollapseDuration = 0.12f;
    public const float TotalDuration = HoldDuration + CollapseDuration;

    private Image segment;
    private float current;
    private float end;
    private float collapseFrom;
    private float elapsed;
    private int previousMax;
    private bool initialized;

    public static HealthDamageTrail Create(Transform parent, Vector2 padding)
    {
        var root = new GameObject("DamageTrailTrack", typeof(RectTransform), typeof(HealthDamageTrail));
        root.layer = parent.gameObject.layer;
        var rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = padding;
        rect.offsetMax = -padding;
        var imageObject = new GameObject("DamageTrail", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.layer = root.layer;
        imageObject.transform.SetParent(root.transform, false);
        var trail = root.GetComponent<HealthDamageTrail>();
        trail.segment = imageObject.GetComponent<Image>();
        trail.segment.color = Color.white;
        trail.segment.raycastTarget = false;
        trail.segment.enabled = false;
        return trail;
    }

    public void SetHealth(int health, int maxHealth)
    {
        float next = maxHealth > 0 ? Mathf.Clamp01((float)health / maxHealth) : 0f;
        if (!initialized || maxHealth != previousMax || next > current)
        {
            current = end = next;
            elapsed = TotalDuration;
        }
        else if (next < current)
        {
            end = Mathf.Max(end, current);
            collapseFrom = end;
            current = next;
            elapsed = 0f;
        }
        previousMax = maxHealth;
        initialized = true;
        Apply();
    }

    public void Clear()
    {
        initialized = false;
        current = end = 0f;
        elapsed = TotalDuration;
        if (segment != null) segment.enabled = false;
    }

    private void Update()
    {
        if (!initialized || end <= current ||
            (GameManager.Instance != null && GameManager.Instance.IsPaused)) return;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= HoldDuration)
        {
            float progress = Mathf.Clamp01((elapsed - HoldDuration) / CollapseDuration);
            end = Mathf.Lerp(collapseFrom, current, progress);
            if (progress >= 1f) end = current;
        }
        Apply();
    }

    private void Apply()
    {
        if (segment == null) return;
        var rect = segment.rectTransform;
        rect.anchorMin = new Vector2(current, 0f);
        rect.anchorMax = new Vector2(end, 1f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        segment.enabled = end > current;
    }

    private void OnDisable() => Clear();
}
