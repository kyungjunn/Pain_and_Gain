using UnityEngine;

public class EquipItem : ItemBase
{
    protected override void ApplyEffect(GameObject player)
    {
        float bonusAttackDamage = itemData.BonusAttackDamage;
        string itemName = itemData.ItemName;

        if (player != null)
        {
            PlayerStats stats = player.GetComponent<PlayerStats>();
           if (stats != null)
            {
                stats.ApplyStat(AugmentType.AttackDamage, bonusAttackDamage);
                Debug.Log($"[장비 적용] '{itemName}' 획득 - {player.name} 에게 공격력 +{bonusAttackDamage} 증가");
            }
            else
            {
                Debug.LogWarning($"[장비 적용 실패] '{itemName}' - {player.name} 에 PlayerStats 컴포넌트가 없습니다.");
            }
            
        }
        else
        {
            Debug.Log($"[테스트] '{itemName}' 가져온 공격력: {bonusAttackDamage}");
        }
    }
}
