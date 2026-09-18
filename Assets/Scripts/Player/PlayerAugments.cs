using UnityEngine;
using System;
using System.Collections.Generic;

// 스킬증강 획득 상태
public enum SkillAcquireStatus
{
    Invalid,        // 잘못된거
    AlreadyMax,     // 이미 최대 중첩
    Instantiated,   // 처음획득이라 생성
    Stacked         // 중첩 증가
}

// 플레이어가 보유한 스킬 증강 관리.
public class PlayerAugments : MonoBehaviour
{
    // 보유 스킬 목록 (직접 수정 X)
    private readonly Dictionary<SkillAugmentSO, AugmentSkill> ownedSkills = new Dictionary<SkillAugmentSO, AugmentSkill>();

    // 보유 스킬 목록 외부에 제공
    public IReadOnlyCollection<SkillAugmentSO> OwnedSkills => ownedSkills.Keys;

    // 스킬목록이나 중첩 변경 이벤트
    public Action onSkillsChanged;

    public bool HasSkill(SkillAugmentSO so)
    {
        return so != null && ownedSkills.ContainsKey(so);
    }

    public bool IsAtMaxStacks(SkillAugmentSO so)
    {
        if (so == null || !ownedSkills.TryGetValue(so, out AugmentSkill skill))
        {
            return false;
        }

        return so.maxStacks > 0 && skill.StackCount >= so.maxStacks;
    }

    public int GetSkillStackCount(SkillAugmentSO so)
    {
        return so != null && ownedSkills.TryGetValue(so, out AugmentSkill skill)
            ? skill.StackCount
            : 0;
    }

    // 획득 또는 중첩
    public SkillAcquireStatus TryAcquireLoadedSkill(SkillAugmentSO so, GameObject prefab)
    {
        // 데이터 검사
        if (so == null)
        {
            return SkillAcquireStatus.Invalid;
        }

        // 최대 중첩 검사
        if (IsAtMaxStacks(so))
        {
            return SkillAcquireStatus.AlreadyMax;
        }

        // 기존 스킬 중첩
        if (ownedSkills.TryGetValue(so, out AugmentSkill ownedSkill) && ownedSkill != null)
        {
            ownedSkill.AddStack();
            onSkillsChanged?.Invoke();
            return SkillAcquireStatus.Stacked;
        }

        // 프리팹 검사
        if (prefab == null || prefab.GetComponent<AugmentSkill>() == null)
        {
            return SkillAcquireStatus.Invalid;
        }

        // 스킬 생성 (플레이어 자식으로)
        AugmentSkill skill = Instantiate(prefab, transform).GetComponent<AugmentSkill>();
        if (skill == null)
        {
            return SkillAcquireStatus.Invalid;
        }

        // 스킬 적용
        skill.Apply(gameObject);
        ownedSkills[so] = skill;
        onSkillsChanged?.Invoke();
        return SkillAcquireStatus.Instantiated;
    }

    // 무작위 중첩 제거
    public bool TryRemoveRandomSkill(out SkillAugmentSO removed)
    {
        removed = null;

        if (ownedSkills.Count == 0)
        {
            return false;
        }

        List<SkillAugmentSO> keys = new List<SkillAugmentSO>(ownedSkills.Keys);
        removed = keys[UnityEngine.Random.Range(0, keys.Count)];

        AugmentSkill skill = ownedSkills[removed];

        // 중첩이 1인 경우
        if (skill == null || !skill.TryRemoveStack())
        {
            // 스킬 삭제
            ownedSkills.Remove(removed);

            if (skill != null)
            {
                skill.Release();
                Destroy(skill.gameObject);
            }
        }

        onSkillsChanged?.Invoke();
        return true;
    }

    // 전체 효과 해제 (전투 종료 or 플레이어 제거 시)
    public void ReleaseAll()
    {
        foreach (KeyValuePair<SkillAugmentSO, AugmentSkill> pair in ownedSkills)
        {
            if (pair.Value != null)
            {
                pair.Value.Release();
                Destroy(pair.Value.gameObject);
            }
        }

        ownedSkills.Clear();
        onSkillsChanged?.Invoke();
    }

    private void OnDestroy()
    {
        ReleaseAll();

        // 플레이어 사용 불가를 Loader에 전달
        AugmentResourceLoader host = AugmentResourceLoader.Instance;
        if (host != null)
        {
            host.NotifyPlayerUnavailable(gameObject);
        }
    }
}
