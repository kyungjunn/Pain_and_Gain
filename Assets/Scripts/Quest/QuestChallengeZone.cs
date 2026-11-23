using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class QuestChallengeZone : MonoBehaviour
{
    [Header("Visible Areas")]
    [SerializeField] private GameObject warningAreaVisual;
    [SerializeField] private GameObject startAreaVisual;
    [SerializeField] private LineRenderer dwellProgressVisual;

    [Header("Area Sizes")]
    [SerializeField, Min(0.1f)] private float warningRadius = 8f;
    [SerializeField, Min(0.1f)] private float startRadius = 2.5f;
    [SerializeField, Min(0f)] private float verticalTolerance = 2f;

    private const int DwellArcSegments = 48;

    private QuestManager owner;
    private Transform player;
    private float lifetime;
    private float dwellDuration;
    private float dwellElapsed;
    private bool configured;
    private bool startRequested;
    private bool expired;
    private bool noticeVisible;

    public bool IsPlayerInWarningArea { get; private set; }
    public bool IsNoticeVisible => noticeVisible;
    public float RemainingLifetime => lifetime;
    public float DwellElapsed => dwellElapsed;
    public float DwellDuration => dwellDuration;
    public float DwellProgress => dwellDuration > 0f ? Mathf.Clamp01(dwellElapsed / dwellDuration) : 0f;

    public event Action<QuestChallengeZone> onStartRequested;
    public event Action<QuestChallengeZone> onExpired;
    public event Action<QuestChallengeZone> onVisibilityChanged;

    public bool Configure(QuestManager questManager, Transform target, float zoneLifetime, float requiredDwell)
    {
        if (questManager == null || target == null || warningAreaVisual == null || startAreaVisual == null ||
            dwellProgressVisual == null || warningAreaVisual == startAreaVisual)
        {
            Debug.LogError("QuestChallengeZone requires an owner, player, distinct area visuals, and a dwell progress LineRenderer.", this);
            return false;
        }

        owner = questManager;
        player = target;
        lifetime = Mathf.Max(0f, zoneLifetime);
        dwellDuration = Mathf.Max(0f, requiredDwell);
        dwellElapsed = 0f;
        IsPlayerInWarningArea = false;
        noticeVisible = false;
        configured = true;

        ConfigureVisual(warningAreaVisual.transform, warningRadius);
        ConfigureVisual(startAreaVisual.transform, startRadius);
        dwellProgressVisual.useWorldSpace = true;
        dwellProgressVisual.positionCount = DwellArcSegments + 1;
        dwellProgressVisual.enabled = false;
        warningAreaVisual.SetActive(true);
        startAreaVisual.SetActive(true);
        return true;
    }

    public void SetNoticeVisible(bool visible)
    {
        bool shouldBeVisible = visible && configured && !expired && !startRequested &&
            IsPlayerInWarningArea && owner != null && !owner.IsQuestTimerPaused;
        if (noticeVisible == shouldBeVisible)
        {
            if (!shouldBeVisible)
            {
                ResetDwellProgress();
            }

            return;
        }

        noticeVisible = shouldBeVisible;
        if (!noticeVisible)
        {
            ResetDwellProgress();
        }
    }

    private void LateUpdate()
    {
        if (!configured || owner == null || player == null || expired || startRequested)
        {
            return;
        }

        Vector3 offset = player.position - transform.position;
        bool withinVerticalTolerance = Mathf.Abs(offset.y) <= verticalTolerance;
        offset.y = 0f;
        float distanceSquared = offset.sqrMagnitude;
        bool inWarningArea = withinVerticalTolerance && distanceSquared <= warningRadius * warningRadius;
        bool inStartArea = withinVerticalTolerance && distanceSquared <= startRadius * startRadius;
        if (IsPlayerInWarningArea != inWarningArea)
        {
            IsPlayerInWarningArea = inWarningArea;
            onVisibilityChanged?.Invoke(this);
        }

        if (!inWarningArea && noticeVisible)
        {
            SetNoticeVisible(false);
        }

        if (owner.IsQuestTimerPaused)
        {
            SetNoticeVisible(false);
            return;
        }

        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            return;
        }

        lifetime = Mathf.Max(0f, lifetime - deltaTime);
        if (inStartArea && inWarningArea && noticeVisible)
        {
            dwellElapsed = Mathf.Min(dwellDuration, dwellElapsed + deltaTime);
        }
        else
        {
            dwellElapsed = 0f;
        }
        UpdateDwellProgressVisual();

        if (dwellElapsed >= dwellDuration)
        {
            startRequested = true;
            onStartRequested?.Invoke(this);
            return;
        }

        if (lifetime <= 0f)
        {
            expired = true;
            onExpired?.Invoke(this);
        }
    }

    private void ConfigureVisual(Transform visual, float radius)
    {
        Vector3 scale = visual.localScale;
        scale.x = radius * 2f;
        scale.z = radius * 2f;
        visual.localScale = scale;
    }

    private void OnValidate()
    {
        warningRadius = Mathf.Max(0.1f, warningRadius);
        startRadius = Mathf.Clamp(startRadius, 0.1f, warningRadius);
        verticalTolerance = Mathf.Max(0f, verticalTolerance);
    }

    private void ResetDwellProgress()
    {
        dwellElapsed = 0f;
        UpdateDwellProgressVisual();
    }

    private void UpdateDwellProgressVisual()
    {
        if (dwellProgressVisual == null)
        {
            return;
        }

        float progress = noticeVisible && IsPlayerInWarningArea ? DwellProgress : 0f;
        dwellProgressVisual.enabled = progress > 0f;
        if (progress <= 0f)
        {
            return;
        }

        float endAngle = 360f * progress;
        Vector3 center = transform.position;
        center.y += 0.05f;
        for (int i = 0; i <= DwellArcSegments; i++)
        {
            float angle = (-90f + endAngle * i / DwellArcSegments) * Mathf.Deg2Rad;
            Vector3 point = center + new Vector3(Mathf.Cos(angle) * startRadius, 0f, Mathf.Sin(angle) * startRadius);
            dwellProgressVisual.SetPosition(i, point);
        }
    }

    private void OnDisable()
    {
        noticeVisible = false;
        dwellElapsed = 0f;
        if (dwellProgressVisual != null)
        {
            dwellProgressVisual.enabled = false;
        }
    }
}
