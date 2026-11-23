using UnityEngine;

[CreateAssetMenu(fileName = "ExperienceReward", menuName = "Game/Quest Rewards/Experience")]
public sealed class ExperienceRewardSO : QuestRewardSO
{
    public override string Description => $"경험치 {amount}";
    public override string DescribeGranted(float grantedAmount) => $"경험치 {grantedAmount:0} 획득";
    protected override bool CanApplyTo(GameObject player) => player.GetComponent<PlayerLevelSystem>() != null;
    protected override float Grant(GameObject player)
    {
        player.GetComponent<PlayerLevelSystem>().AddExp(amount);
        return amount;
    }
}
