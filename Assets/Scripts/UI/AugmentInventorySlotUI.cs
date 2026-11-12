using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 증강 이름 및 중첩 표시 · 호버/선택 시 설명 표시
public sealed class AugmentInventorySlotUI : MonoBehaviour, IPointerEnterHandler,
    IPointerExitHandler, IPointerClickHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI levelText;
    public SkillAugmentSO Augment { get; private set; }
    public int Stacks { get; private set; }
    internal TextMeshProUGUI NameText => nameText;
    private Selectable selectable;
    private Outline focusOutline;
    private AugmentTooltipUI tooltip;

    private void Awake()
    {
        EnsureSelectable();
    }

    private void EnsureSelectable()
    {
        if (selectable == null)
        {
            selectable = GetComponent<Selectable>();
            if (selectable == null)
            {
                selectable = gameObject.AddComponent<Selectable>();
                selectable.transition = Selectable.Transition.None;
            }
        }
        var background = GetComponent<Image>();
        if (background == null)
        {
            background = gameObject.AddComponent<Image>();
            background.color = Color.clear;
        }
        background.raycastTarget = true;
        selectable.targetGraphic = background;
        selectable.interactable = Augment != null;
        if (focusOutline == null)
        {
            focusOutline = GetComponent<Outline>();
            if (focusOutline == null) focusOutline = gameObject.AddComponent<Outline>();
            focusOutline.effectColor = new Color(0.45f, 0.85f, 1f, 1f);
            focusOutline.effectDistance = new Vector2(2f, -2f);
            focusOutline.useGraphicAlpha = false;
            focusOutline.enabled = false;
        }
    }

    public void Configure(Image icon, TextMeshProUGUI nameText, TextMeshProUGUI levelText)
    {
        ClearTooltip();
        this.icon = icon;
        this.nameText = nameText;
        this.levelText = levelText;
    }

    public void SetAugment(SkillAugmentSO augment, int stacks = 0)
    {
        ClearTooltip();
        Augment = augment;
        Stacks = stacks;
        EnsureSelectable();
        icon.sprite = augment != null ? augment.icon : null;
        icon.enabled = icon.sprite != null;
        icon.raycastTarget = false;
        nameText.text = augment != null ? augment.augmentName : string.Empty;
        // 무제한 또는 복수 중첩 증강만 레벨 표시
        bool stackable = augment != null && (augment.maxStacks == 0 || augment.maxStacks > 1);
        levelText.text = stackable ? $"Lv.{stacks}" : string.Empty;
        levelText.gameObject.SetActive(stackable);
    }

    private AugmentTooltipUI GetTooltip()
    {
        if (tooltip == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null) tooltip = AugmentTooltipUI.ForCanvas(canvas.rootCanvas);
        }
        return tooltip;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Augment != null && isActiveAndEnabled)
            GetTooltip()?.SetHovered(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltip != null) tooltip.ClearHovered(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || Augment == null || !isActiveAndEnabled) return;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject, eventData);
        if (focusOutline != null) focusOutline.enabled = true;
        GetTooltip()?.SetFocused(this);
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (Augment != null && isActiveAndEnabled)
        {
            if (focusOutline != null) focusOutline.enabled = true;
            GetTooltip()?.SetFocused(this);
        }
    }

    public void OnDeselect(BaseEventData eventData)
    {
        if (focusOutline != null) focusOutline.enabled = false;
        if (tooltip != null) tooltip.ClearFocused(this);
    }

    private void ClearTooltip()
    {
        if (focusOutline != null) focusOutline.enabled = false;
        if (tooltip == null) return;
        tooltip.ClearHovered(this);
        tooltip.ClearFocused(this);
    }

    private void OnDisable()
    {
        ClearTooltip();
    }
}
