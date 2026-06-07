using UnityEngine;
using System;

public class PlayerStats : MonoBehaviour
{
    [Header("Base Stats")] // 기본 스탯
    public float baseAttackDamage = 10f;
    public float baseHP = 100f;
    public float baseMoveSpeed = 5f;
    public float baseAttackSpeed = 1f;
    public float baseDefense = 0f;

    [Header("Augment Stats")] // 증가되는 스탯
    private float AugmentAttackDamage;
    private float AugmentHP;
    private float AugmentMoveSpeed;
    private float AugmentAttackSpeed;
    private float AugmentDefense;

    // 스탯 변경 이벤트
    public Action onStatsChanged;

    // 최종 계산 스탯
    // 프로퍼티 사용 -> 외부에서 수정 불가능 설계
    public float AttackDamage => baseAttackDamage + AugmentAttackDamage;
    public float HP => baseHP + AugmentHP;
    public float MoveSpeed => baseMoveSpeed + AugmentMoveSpeed;
    public float AttackSpeed => baseAttackSpeed + AugmentAttackSpeed;
    public float Defense => baseDefense + AugmentDefense;

    public void ApplyStat(AugmentType type, float value)
    {
        switch (type)
        {
            case AugmentType.AttackDamage:
                AugmentAttackDamage += value;
                break;

            case AugmentType.HP:
                AugmentHP += value;
                break;

            case AugmentType.MoveSpeed:
                AugmentMoveSpeed += value;
                break;

            case AugmentType.AttackSpeed:
                AugmentAttackSpeed += value;
                break;

            case AugmentType.Defense:
                AugmentDefense += value;
                break;
        }

        // 스탯 갱신 이벤트 호출
        onStatsChanged?.Invoke();
    }
}