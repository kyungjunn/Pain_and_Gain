using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CombatDamageNumbers : MonoBehaviour
{
    private const int PoolCapacity = 24;
    private const float Lifetime = 0.9f;
    private const float RiseDistance = 68f;
    private const float FadeStart = 0.58f;
    private static readonly Color DealtDamageColor = new Color(1f, 0.82f, 0.32f, 1f);
    private static readonly Color ReceivedDamageColor = new Color(1f, 0.28f, 0.22f, 1f);

    private Canvas damageCanvas;
    private RectTransform canvasRect;
    private Popup[] pool;
    private GameManager sessionManager;
    private int nextPopupIndex;
    private int sessionId;
    private bool sessionEnded;

    private sealed class Popup
    {
        public RectTransform rect;
        public TextMeshProUGUI text;
        public Vector2 startPosition;
        public Color color;
        public float elapsed;
    }

    private void Awake()
    {
        sessionManager = GameManager.Instance;
        sessionId = sessionManager != null ? sessionManager.SessionId : 0;
        CreatePool();
    }

    private void OnEnable()
    {
        if (sessionEnded)
            return;

        if (damageCanvas == null)
            CreatePool();

        if (sessionManager != null && sessionId != 0)
        {
            sessionManager.OnCombatSessionEnding -= HandleSessionEnding;
            sessionManager.OnCombatSessionEnding += HandleSessionEnding;
        }
    }

    private void OnDisable()
    {
        if (sessionManager != null)
            sessionManager.OnCombatSessionEnding -= HandleSessionEnding;
        TearDownPool();
    }

    private void OnDestroy()
    {
        TearDownPool();
    }

    private void Update()
    {
        if (sessionId != 0 &&
            (GameManager.Instance == null || !GameManager.Instance.BelongsToSession(sessionId)))
        {
            sessionEnded = true;
            enabled = false;
            return;
        }

        if (pool == null || (GameManager.Instance != null && GameManager.Instance.IsPaused))
            return;

        float deltaTime = Time.unscaledDeltaTime;
        if (deltaTime <= 0f)
            return;

        foreach (Popup popup in pool)
        {
            if (!popup.rect.gameObject.activeSelf)
                continue;

            popup.elapsed += deltaTime;
            float progress = popup.elapsed / Lifetime;
            if (progress >= 1f)
            {
                popup.rect.gameObject.SetActive(false);
                continue;
            }

            float easedProgress = 1f - (1f - progress) * (1f - progress);
            popup.rect.anchoredPosition = popup.startPosition + Vector2.up * (RiseDistance * easedProgress);
            popup.rect.localScale = Vector3.one * Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(popup.elapsed / 0.12f));

            float alpha = progress <= FadeStart
                ? 1f
                : Mathf.Clamp01((1f - progress) / (1f - FadeStart));
            Color color = popup.color;
            color.a *= alpha;
            popup.text.color = color;
        }
    }

    public void ShowDamage(int actualDamage, Vector3 worldPosition, bool received)
    {
        if (actualDamage <= 0 || !isActiveAndEnabled || pool == null || canvasRect == null)
            return;

        Camera camera = Camera.main;
        if (camera == null)
            return;

        Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);
        if (screenPosition.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPosition, null, out Vector2 localPosition))
            return;

        Popup popup = GetNextPopup();
        popup.elapsed = 0f;
        popup.startPosition = localPosition;
        popup.color = received ? ReceivedDamageColor : DealtDamageColor;
        popup.text.text = received ? "-" + actualDamage : actualDamage.ToString();
        popup.text.color = popup.color;
        popup.rect.anchoredPosition = localPosition;
        popup.rect.localScale = Vector3.one * 0.65f;
        popup.rect.gameObject.SetActive(true);
    }

    private void CreatePool()
    {
        if (pool != null)
            return;

        GameObject canvasObject = new GameObject("CombatDamageNumbers",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        if (gameObject.scene.IsValid() && gameObject.scene.isLoaded)
            SceneManager.MoveGameObjectToScene(canvasObject, gameObject.scene);

        damageCanvas = canvasObject.GetComponent<Canvas>();
        damageCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        damageCanvas.overrideSorting = true;
        damageCanvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        canvasRect = canvasObject.GetComponent<RectTransform>();

        pool = new Popup[PoolCapacity];
        for (int i = 0; i < pool.Length; i++)
        {
            GameObject textObject = new GameObject("DamageNumber",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(180f, 64f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 36f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.outlineColor = Color.black;
            text.outlineWidth = 0.18f;
            text.fontMaterial.EnableKeyword("OUTLINE_ON");
            textObject.SetActive(false);

            pool[i] = new Popup
            {
                rect = rect,
                text = text
            };
        }
    }

    private Popup GetNextPopup()
    {
        for (int offset = 0; offset < pool.Length; offset++)
        {
            int index = (nextPopupIndex + offset) % pool.Length;
            Popup popup = pool[index];
            if (!popup.rect.gameObject.activeSelf)
            {
                nextPopupIndex = (index + 1) % pool.Length;
                return popup;
            }
        }

        int oldestIndex = 0;
        for (int i = 1; i < pool.Length; i++)
        {
            if (pool[i].elapsed > pool[oldestIndex].elapsed)
                oldestIndex = i;
        }

        nextPopupIndex = (oldestIndex + 1) % pool.Length;
        return pool[oldestIndex];
    }

    private void HandleSessionEnding(int endingSessionId)
    {
        if (endingSessionId != sessionId)
            return;

        sessionEnded = true;
        enabled = false;
    }

    private void TearDownPool()
    {
        if (damageCanvas != null)
            Destroy(damageCanvas.gameObject);

        damageCanvas = null;
        canvasRect = null;
        pool = null;
        nextPopupIndex = 0;
    }
}
