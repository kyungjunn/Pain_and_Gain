using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class AugmentManager : MonoBehaviour
{
    public static AugmentManager Instance;

    private PlayerStats playerStats;

    [Header("All Augments")]
    public List<StatAugmentSO> allAugments;

    [Header("Settings")]
    public int optionCount = 3;

    private void Awake()
    {
        Instance = this;
    }
    private void OnEnable()
    {
        SpawnManager.OnPlayerSpawned += HandlePlayerSpawned;
    }
    private void OnDisable()
    {
        SpawnManager.OnPlayerSpawned -= HandlePlayerSpawned;
    }

    // 플레이어 스폰 시 실행되는 콜백함수
    private void HandlePlayerSpawned(GameObject playerObject)
    {
        playerStats = playerObject.GetComponent<PlayerStats>();
    }

    public List<StatAugmentSO> GetRandomAugments()
    {
        // 최종 증강 리스트
        List<StatAugmentSO> result = new List<StatAugmentSO>();

        // 원본 훼손하지 않기 위한 복사본
        List<StatAugmentSO> pool = new List<StatAugmentSO>(allAugments);

        for (int i = 0; i < optionCount; i++)
        {
            StatAugmentSO selected = GetWeightedRandom(pool);

            // 리스트에 추가
            result.Add(selected);

            // 중복 제거(선택된 증강 제거)
            pool.Remove(selected);
        }

        return result;
    }

    // 가중치 기반 랜덤 선택 함수
    private StatAugmentSO GetWeightedRandom(List<StatAugmentSO> pool)
    {
        // 전체 weight 합 
        int totalWeight = pool.Sum(x => x.weight);
        // 예외 처리
        if (totalWeight <= 0)
        {
            return pool[0];
        }

        int random = Random.Range(0, totalWeight);

        int current = 0;

        // 누적합 비교
        foreach (var augment in pool)
        {
            current += augment.weight;

            // 랜덤값이 현재 범위 안에 들어왔다면
            if (random < current)
            {
                return augment;
            }
        }

        return pool[0];
    }

    public void ApplyAugment(StatAugmentSO augment)
    {
        if (playerStats == null)
        {
            Debug.LogError("PlayerStats 바인딩 안됨.");
            return;
        }

        playerStats.ApplyStat(augment.type, augment.value);
    }
}