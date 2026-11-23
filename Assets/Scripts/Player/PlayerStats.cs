using UnityEngine;
using System;
using System.Collections.Generic;

public class PlayerStats : MonoBehaviour
{
    [Header("Base Stats")] // 기본 스탯
    public float baseAttackDamage = 10f;
    public float baseHP = 100f;
    public float baseMoveSpeed = 5f;
    public float baseAttackSpeed = 1f;
    public float baseDefense = 0f;

    // 보너스 통 3개. 장비 보너스가 증강 통에 섞이면 착용·해제 시 증강 스탯이 유실되므로 분리한다.
    private readonly Dictionary<AugmentType, float> augmentBonus = new Dictionary<AugmentType, float>(); // 영구. 퀘스트 페널티로만 감소
    private readonly Dictionary<AugmentType, float> equipBonus = new Dictionary<AugmentType, float>();   // 장비 슬롯 상태에서 재계산
    private readonly Dictionary<AugmentType, float> tempBonus = new Dictionary<AugmentType, float>();    // 시간 만료 버프
    private readonly Dictionary<object, Dictionary<AugmentType, float>> sourcedTempBonus =
        new Dictionary<object, Dictionary<AugmentType, float>>();

    // 스탯 변경 이벤트
    public Action onStatsChanged;

    // 최종 계산 스탯
    // 프로퍼티 사용 -> 외부에서 수정 불가능 설계
    public float AttackDamage => baseAttackDamage + GetTotalBonus(AugmentType.AttackDamage);
    public float HP => baseHP + GetTotalBonus(AugmentType.HP);
    public float MoveSpeed => baseMoveSpeed + GetTotalBonus(AugmentType.MoveSpeed);
    public float AttackSpeed => baseAttackSpeed + GetTotalBonus(AugmentType.AttackSpeed);
    public float Defense => baseDefense + GetTotalBonus(AugmentType.Defense);

    // 증강 획득. 누적분에 더한다.
    public void ApplyStat(AugmentType type, float value)
    {
        augmentBonus[type] = GetAugmentBonus(type) + value;

        // 스탯 갱신 이벤트 호출
        onStatsChanged?.Invoke();
    }

    // 증강 누적분을 비율만큼 감소시키고 실제 감소량을 반환.
    // 증강 누적치만 비례 감소: 기본 스탯 유지·음수 방지
    public float ReduceStatByRatio(AugmentType type, float ratio)
    {
        float current = GetAugmentBonus(type);
        float amount = current * Mathf.Clamp01(ratio);

        if (amount <= 0f)
        {
            return 0f;
        }

        augmentBonus[type] = current - amount;
        onStatsChanged?.Invoke();

        return amount;
    }

    // 증강 누적치 > 0인 스탯 = 박탈 후보
    public IReadOnlyList<AugmentType> GetAugmentedStatTypes()
    {
        List<AugmentType> result = new List<AugmentType>();

        foreach (KeyValuePair<AugmentType, float> pair in augmentBonus)
        {
            if (pair.Value > 0f)
            {
                result.Add(pair.Key);
            }
        }

        return result;
    }

    // 현재 증강 누적분 조회
    public float GetAugmentBonus(AugmentType type)
    {
        return GetBonus(augmentBonus, type);
    }

    // 장비 보너스는 착용/해제 짝 맞추기가 아니라 슬롯 상태 기준의 절대값으로 덮어쓴다.
    public void SetEquipBonus(AugmentType type, float value)
    {
        equipBonus[type] = value;
        onStatsChanged?.Invoke();
    }

    // 임시 버프 절대값 설정: 해제 시 영으로 초기화
    public void SetTempBonus(AugmentType type, float value)
    {
        tempBonus[type] = value;
        onStatsChanged?.Invoke();
    }

    // 스킬별 임시 보너스: 소모품 버프와 독립 합산
    public void SetTemporaryBonus(object source, AugmentType type, float value)
    {
        if (source == null)
        {
            return;
        }

        if (!sourcedTempBonus.TryGetValue(source, out Dictionary<AugmentType, float> sourceBonus))
        {
            sourceBonus = new Dictionary<AugmentType, float>();
            sourcedTempBonus.Add(source, sourceBonus);
        }

        sourceBonus[type] = value;
        onStatsChanged?.Invoke();
    }

    public void RemoveTemporaryBonuses(object source)
    {
        if (source != null && sourcedTempBonus.Remove(source))
        {
            onStatsChanged?.Invoke();
        }
    }

    private float GetTotalBonus(AugmentType type)
    {
        // 증강·장비·소모품·스킬 보너스 합산
        return GetBonus(augmentBonus, type) + GetBonus(equipBonus, type) + GetBonus(tempBonus, type) +
               GetSourcedTemporaryBonus(type);
    }

    private float GetSourcedTemporaryBonus(AugmentType type)
    {
        float total = 0f;
        foreach (KeyValuePair<object, Dictionary<AugmentType, float>> sourceBonus in sourcedTempBonus)
        {
            total += GetBonus(sourceBonus.Value, type);
        }

        return total;
    }

    private static float GetBonus(Dictionary<AugmentType, float> bonus, AugmentType type)
    {
        return bonus.TryGetValue(type, out float value) ? value : 0f;
    }
}
