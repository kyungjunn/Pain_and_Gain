using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class AugmentOptionUI : MonoBehaviour
{
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descText;
    public Image iconImage;

    private StatAugmentSO currentAugment;
    private AugmentManager manager;

    public void Setup(
        StatAugmentSO augment,
        AugmentManager augmentManager)
    {
        currentAugment = augment;
        manager = augmentManager;

        titleText.text =
            $"{augment.augmentName} +{augment.value}";

        descText.text = augment.description;

        iconImage.sprite = augment.icon;
    }

    public void OnClick()
    {
        manager.ApplyAugment(currentAugment);
    }
}