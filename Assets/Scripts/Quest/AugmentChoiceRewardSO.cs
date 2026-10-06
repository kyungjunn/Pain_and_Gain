using UnityEngine;

[CreateAssetMenu(fileName = "AugmentChoiceReward", menuName = "Game/Quest Rewards/Augment Choice")]
public sealed class AugmentChoiceRewardSO : QuestRewardSO
{
    public override string Description => $"증강 선택 {amount}회";
    // 선택 UI 및 활성 증강 세션 확인
    protected override bool CanApplyTo(GameObject player) => UIManager.Instance != null
        && UIManager.Instance.augmentPanelUI != null
        && AugmentManager.Instance != null
        && AugmentResourceLoader.Instance != null
        && AugmentResourceLoader.Instance.IsActiveSession;
    protected override float Grant(GameObject player)
    {
        for (int i = 0; i < amount; i++)
            UIManager.Instance.RequestAugmentChoice();
        return amount;
    }
}
