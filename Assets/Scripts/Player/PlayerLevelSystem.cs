using UnityEngine;
using System;

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

    public Action onExpChanged;
    public Action onLevelUp;

    public AugmentManager augmentManager;

    public void AddExp(int amount)
    {
        currentExp += amount;

        while (currentExp >= requiredExp)
        {
            currentExp -= requiredExp;
            LevelUp();
        }

        onExpChanged?.Invoke();
    }

    void LevelUp()
    {
        level++;

        // 레벨마다 요구 경험치 증가
        requiredExp *= Mathf.RoundToInt(requiredExp * levelUpExp); 

        onLevelUp?.Invoke();

        Debug.Log($"레벨업! 다음 레벨 요구 경험치: {requiredExp}");

        augmentManager.ShowRandomAugments();
    }

    public float GetExpPercent()
    {
        return currentExp / requiredExp;
    }
}