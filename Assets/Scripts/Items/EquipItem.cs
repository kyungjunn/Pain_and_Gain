using UnityEngine;

public class EquipItem : ItemBase
{
    protected override void ApplyEffect(GameObject player)
    {
        float bonusAttackDamage = itemData.BonusAttackDamage;

        if (player != null)
        {
            Debug.Log($"[장비 적용] {player.name} 에게 데미지 +{bonusAttackDamage} 증가");
        }
        else
        {
            Debug.Log($"[테스트] 가져온 공격력: {bonusAttackDamage}");
        }
    }
}
