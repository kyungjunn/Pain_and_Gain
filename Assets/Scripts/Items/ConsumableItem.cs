using UnityEngine;

public class ConsumableItem : ItemBase
{
    protected override void ApplyEffect(GameObject player)
    {
        int hpRecoveryAmount = Mathf.RoundToInt(itemData.HpRecoveryAmount);
        string itemName = itemData.ItemName;

        if (player != null)
        {
            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                int healedAmount = playerHealth.Heal(hpRecoveryAmount);
                Debug.Log($"[소모품 적용] '{itemName}' 획득 - {player.name} 의 HP {healedAmount}만큼 회복 (요청량: {hpRecoveryAmount})");
            }
            else
            {
                Debug.LogWarning($"[소모품 적용 실패] '{itemName}' - {player.name} 에 PlayerHealth 컴포넌트가 없습니다.");
            }
        }
        else 
        {
            Debug.Log($"[테스트] '{itemName}' 가져온 회복량: {hpRecoveryAmount}");
        }
    }
}
