using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Stats")]
    public float attack = 10f;
    public float hp = 100f;
    public float moveSpeed = 5f;
    public float attackSpeed = 1f;
    public float defense = 0f;

    // 기본 스탯 증가
    public void ApplyStat(AugmentType type, float value)
    {
        switch (type)
        {
            case AugmentType.Attack:
                attack += value;
                break;

            case AugmentType.HP:
                hp += value;
                break;

            case AugmentType.MoveSpeed:
                moveSpeed += value;
                break;

            case AugmentType.AttackSpeed:
                attackSpeed += value;
                break;

            case AugmentType.Defense:
                defense += value;
                break;
        }
    }
}