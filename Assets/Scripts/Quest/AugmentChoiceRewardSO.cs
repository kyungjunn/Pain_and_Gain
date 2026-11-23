using UnityEngine;

[CreateAssetMenu(fileName = "AugmentChoiceReward", menuName = "Game/Quest Rewards/Augment Choice")]
public sealed class AugmentChoiceRewardSO : QuestRewardSO
{
    [Min(0)] public int fallbackExperience;

    public override string Description =>
        $"증강 선택 {amount}회 (후보 소진 시 경험치 {fallbackExperience}; 다음 레벨업에 사용, 런 종료 시 소멸)";

    // 활성 세션·현재 플레이어
    protected override bool CanApplyTo(GameObject player) => UIManager.Instance != null
        && UIManager.Instance.augmentPanelUI != null
        && AugmentManager.Instance != null
        && GameManager.Instance != null
        && GameManager.Instance.IsActiveSession
        && GameManager.Instance.CurrentPlayer == player;
    protected override float Grant(GameObject player)
    {
        int granted = 0;
        for (int i = 0; i < amount; i++)
        {
            if (!UIManager.Instance.DeferAugmentChoice(fallbackExperience))
            {
                break;
            }

            granted++;
        }

        return granted;
    }
}
