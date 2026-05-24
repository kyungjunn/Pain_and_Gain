using UnityEngine;
using System.Collections.Generic;

public class AugmentPanelUI : MonoBehaviour
{
    public List<AugmentOptionUI> optionUIs;

    public void Setup(List<StatAugmentSO> augments)
    {
        for (int i = 0; i < optionUIs.Count; i++)
        {
            optionUIs[i].gameObject.SetActive(false);
        }

        for (int i = 0; i < augments.Count; i++)
        {
            optionUIs[i].gameObject.SetActive(true);

            optionUIs[i].Setup(augments[i]);
        }
    }
}