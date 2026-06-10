using UnityEngine;

public class ConsumableItem : ItemBase
{
    protected override void ApplyEffect(GameObject player)
    {
        float hpRecoveryAmount = itemData.HpRecoveryAmount;

        if (player != null)
        {
            Debug.Log($"[소모품 적용] {player.name} 의 HP 회복량 +{hpRecoveryAmount}만큼 증가");
        }
        else 
        {
            Debug.Log($"[테스트] 가져온 회복량: {hpRecoveryAmount}");
        }
    }
}
