using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// 스탯 페널티 결과. HUD 연출에 필요한 최소 정보만 담는다.
public struct StatReduceResult
{
    public AugmentType type;    // 어떤 스탯이 깎였는지
    public float amount;        // 실제로 깎인 수치
}

public class AugmentManager : MonoBehaviour
{
    public static AugmentManager Instance;

    private PlayerStats playerStats;
    private PlayerAugments playerAugments;

    [Header("All Augments")]
    public List<AugmentSO> allAugments;

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
        playerAugments = playerObject.GetComponent<PlayerAugments>();
    }

    public List<AugmentSO> GetRandomAugments()
    {
        // 최종 증강 리스트
        List<AugmentSO> result = new List<AugmentSO>();

        // 원본 훼손하지 않기 위한 복사본
        List<AugmentSO> pool = new List<AugmentSO>(allAugments);

        // 스킬은 중복 획득이 의미 없으므로 이미 보유한 스킬은 추첨 풀에서 제외
        if (playerAugments != null)
        {
            pool.RemoveAll(x => x is SkillAugmentSO skill && playerAugments.HasSkill(skill));
        }

        for (int i = 0; i < optionCount && pool.Count > 0; i++)
        {
            AugmentSO selected = GetWeightedRandom(pool);

            // 리스트에 추가
            result.Add(selected);

            // 중복 제거(선택된 증강 제거)
            pool.Remove(selected);
        }

        return result;
    }

    // 가중치 기반 랜덤 선택 함수
    private AugmentSO GetWeightedRandom(List<AugmentSO> pool)
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

    public void ApplyAugment(AugmentSO augment)
    {
        switch (augment)
        {
            case StatAugmentSO stat:
                if (playerStats == null)
                {
                    Debug.LogError("PlayerStats 바인딩 안됨.");
                    return;
                }

                playerStats.ApplyStat(stat.type, stat.value);
                break;

            case SkillAugmentSO skill:
                if (playerAugments == null)
                {
                    Debug.LogError("PlayerAugments 바인딩 안됨.");
                    return;
                }

                playerAugments.AddSkill(skill);
                break;
        }
    }

    // ---- 페널티 창구 ----
    // 플레이어 바인딩 배관이 이미 여기 있으므로 퀘스트 시스템은 이 두 함수만 호출한다.

    // 보유 스킬 중 랜덤 1개 박탈. 스킬이 없으면 null
    public SkillAugmentSO RemoveRandomSkill()
    {
        if (playerAugments == null)
        {
            return null;
        }

        return playerAugments.TryRemoveRandomSkill(out SkillAugmentSO removed) ? removed : null;
    }

    // 증강분 > 0인 스탯 중 랜덤 1개를 minRatio~maxRatio 비율로 감소. 깎을 스탯이 없으면 null
    public StatReduceResult? ReduceRandomStat(float minRatio, float maxRatio)
    {
        if (playerStats == null)
        {
            return null;
        }

        IReadOnlyList<AugmentType> candidates = playerStats.GetAugmentedStatTypes();

        if (candidates.Count == 0)
        {
            return null;
        }

        AugmentType type = candidates[Random.Range(0, candidates.Count)];
        float amount = playerStats.ReduceStatByRatio(type, Random.Range(minRatio, maxRatio));

        return new StatReduceResult { type = type, amount = amount };
    }

    // ---- 에디터 테스트 진입점 (플레이 중 컴포넌트 우클릭) ----

    [ContextMenu("Test/Remove Random Skill")]
    private void TestRemoveRandomSkill()
    {
        SkillAugmentSO removed = RemoveRandomSkill();
        Debug.Log(removed != null ? $"[Penalty] 스킬 박탈: {removed.augmentName}" : "[Penalty] 박탈할 스킬 없음");
    }

    [ContextMenu("Test/Reduce Random Stat 25~50%")]
    private void TestReduceRandomStat()
    {
        StatReduceResult? result = ReduceRandomStat(0.25f, 0.5f);
        Debug.Log(result.HasValue ? $"[Penalty] 스탯 감소: {result.Value.type} -{result.Value.amount}" : "[Penalty] 깎을 스탯 없음");
    }
}
