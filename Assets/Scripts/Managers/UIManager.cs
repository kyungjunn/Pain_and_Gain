using UnityEngine;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Panels")]
    public AugmentPanelUI augmentPanelUI;
    public StatsPanelUI statsPanelUI;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void OpenAugmentPanel(
        List<StatAugmentSO> augments)
    {
        augmentPanelUI.gameObject.SetActive(true);

        augmentPanelUI.Setup(augments);

        GameManager.Instance.PauseGame();
    }

    public void CloseAugmentPanel()
    {
        augmentPanelUI.gameObject.SetActive(false);

        GameManager.Instance.ResumeGame();
    }
}