using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class AugmentOptionUI : MonoBehaviour
{
    public Image iconImage;
    public Image backgroundImage;

    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descText;
    public TextMeshProUGUI rarityText;

    private StatAugmentSO currentAugment;

    public void Setup(StatAugmentSO augment)
    {
        currentAugment = augment;

        titleText.text =
            $"{augment.augmentName} +{augment.value}";

        descText.text =
            augment.description;

        rarityText.text =
            augment.rarity.ToString();

        iconImage.sprite =
            augment.icon;

        SetRarityColor(augment.rarity);
    }

    void SetRarityColor(RarityType rarity)
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
                backgroundImage.color =
                    new Color(0.6f, 0f, 1f);
                break;

            case RarityType.Legendary:
                backgroundImage.color =
                    new Color(1f, 0.5f, 0f);
                break;
        }
    }

    public void OnClick()
    {
        AugmentManager.Instance
            .ApplyAugment(currentAugment);

        UIManager.Instance
            .CloseAugmentPanel();
    }
}