using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class AugmentManager : MonoBehaviour
{
    public static AugmentManager Instance;

    public PlayerStats playerStats;

    [Header("All Augments")]
    public List<StatAugmentSO> allAugments;

    [Header("Settings")]
    public int optionCount = 3;

    private void Awake()
    {
        Instance = this;
    }

    public List<StatAugmentSO> GetRandomAugments()
    {
        List<StatAugmentSO> result =
            new List<StatAugmentSO>();

        List<StatAugmentSO> pool =
            new List<StatAugmentSO>(allAugments);

        for (int i = 0; i < optionCount; i++)
        {
            StatAugmentSO selected =
                GetWeightedRandom(pool);

            result.Add(selected);

            // 중복 제거
            pool.Remove(selected);
        }

        return result;
    }

    private StatAugmentSO GetWeightedRandom(
        List<StatAugmentSO> pool)
    {
        int totalWeight =
            pool.Sum(x => x.weight);

        int random =
            Random.Range(0, totalWeight);

        int current = 0;

        foreach (var augment in pool)
        {
            current += augment.weight;

            if (random < current)
            {
                return augment;
            }
        }

        return pool[0];
    }

    public void ApplyAugment(StatAugmentSO augment)
    {
        playerStats.ApplyStat(
            augment.type,
            augment.value
        );
    }
}