using UnityEngine;
using TMPro;
using UnityEngine.UI;

// 증강 카드 하나의 표시와 선택 처리를 담당
public class AugmentOptionUI : MonoBehaviour
{
    [SerializeField] private Button button;

    public Image iconImage;
    public Image backgroundImage;

    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descText;
    public TextMeshProUGUI rarityText;

    private AugmentSO currentAugment;

    private void Awake()
    {
        CacheButton();
    }

    public void Setup(AugmentSO augment)
    {
        currentAugment = augment;

        titleText.text = augment.GetDisplayName();
        descText.text = augment.description;
        rarityText.text = augment.rarity.ToString();
        iconImage.sprite = augment.icon;

        SetRarityColor(augment.rarity);
    }

    public void SetInteractable(bool interactable)
    {
        CacheButton();

        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private void SetRarityColor(RarityType rarity)
    {
        switch (rarity)
        {
            case RarityType.Common:
                backgroundImage.color = Color.white;
                break;

            case RarityType.Rare:
                backgroundImage.color = Color.blue;
                break;

            case RarityType.Epic:
                backgroundImage.color = new Color(0.6f, 0f, 1f);
                break;

            case RarityType.Legendary:
                backgroundImage.color = new Color(1f, 0.5f, 0f);
                break;
        }
    }

    public void OnClick()
    {
        CacheButton();

        // 선택 유예 시간 동안 들어온 클릭은 무시
        if (button != null && !button.interactable)
        {
            return;
        }

        if (currentAugment == null)
        {
            return;
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayLevelUp();
        }

        if (AugmentManager.Instance != null)
        {
            AugmentManager.Instance.ApplyAugment(currentAugment);
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseAugmentPanel();
        }
    }

    private void CacheButton()
    {
        if (button == null)
        {
            button = GetComponentInChildren<Button>(true);
        }
    }
}
