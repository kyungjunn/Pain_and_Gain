using System;
using UnityEngine;

public enum PlayerDamageType
{
    BasicAttack,
    Skill
}

// 플레이어가 적에게 준 실제 피해량과 출처를 한 곳에서 전달한다.
public class PlayerDamageDealer : MonoBehaviour
{
    public event Action<int, PlayerDamageType> OnDamageDealt;

    public int DealDamage(EnemyHealth enemy, int damage, PlayerDamageType damageType)
    {
        if (enemy == null || enemy.IsDead || damage <= 0)
        {
            return 0;
        }

        int actualDamage = Mathf.Min(damage, enemy.CurrentHealth);
        enemy.TakeDamage(damage);

        if (actualDamage > 0)
        {
            OnDamageDealt?.Invoke(actualDamage, damageType);
        }

        return actualDamage;
    }
}
