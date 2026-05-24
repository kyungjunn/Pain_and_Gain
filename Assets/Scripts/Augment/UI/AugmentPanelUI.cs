using UnityEngine;
using System.Collections.Generic;

public class AugmentPanelUI : MonoBehaviour
{
    public List<AugmentOptionUI> optionUIs; // UI 슬롯들

    public void Setup(List<StatAugmentSO> augments) // 랜덤을 뽑힌 데이터
    {
        for (int i = 0; i < optionUIs.Count; i++)
        {
            optionUIs[i].gameObject.SetActive(false);
        }

        for (int i = 0; i < augments.Count; i++)
        {
            optionUIs[i].gameObject.SetActive(true);

            optionUIs[i].Setup(augments[i]); //AugmentOptionUI 의 Setup
        }
    }
}