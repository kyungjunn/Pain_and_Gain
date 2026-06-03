using UnityEngine;
using System;
using System.Collections.Generic;

public class PlayerLevelSystem : MonoBehaviour
{
    // 현재 레벨
    public int level = 1;

    // 현재 경험치
    public int currentExp = 0;

    // 필요한 경험치 
    public int requiredExp = 100;

    // 레벨 업 시 경험치 증가량
    public float levelUpExp = 1.2f;

    public Action onExpChanged; // 경험치 변경 이벤트
    public Action onLevelUp; // 레벨업 이벤트

    public void AddExp(int amount)
    {
        currentExp += amount;

        while (currentExp >= requiredExp)
        {
            currentExp -= requiredExp; // 현재 레벨업에 사용된 경험치 차감
            LevelUp();
        }

        // 경험치 이벤트 호출
        onExpChanged?.Invoke();
    }

    void LevelUp()
    {
        level++;

        // 레벨마다 요구 경험치 증가
        requiredExp = Mathf.RoundToInt(requiredExp * levelUpExp); 

        // 레벨업 이벤트 호출
        onLevelUp?.Invoke();

        if (AugmentManager.Instance != null)
        {
            // 랜덤 증강 선택지 생성
            List<StatAugmentSO> augments = AugmentManager.Instance.GetRandomAugments();

            // 증강 선택 UI 열기
            UIManager.Instance.OpenAugmentPanel(augments);
        }
        else
        {
            Debug.LogError("AugmentManager 인스턴스 찾을 수 없음!");
        }

        Debug.Log($"레벨업! 다음 레벨 요구 경험치: {requiredExp}");
    }

    public float GetExpPercent()
    {
        return currentExp / requiredExp;
    }
}