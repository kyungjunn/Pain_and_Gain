using UnityEngine;
using System;
using System.Collections.Generic;

// 플레이어가 보유한 스킬 증강 관리.
// 이 딕셔너리가 곧 보유 목록이자 박탈 후보 풀이다. 별도 이력 리스트는 두지 않는다.
public class PlayerAugments : MonoBehaviour
{
    private readonly Dictionary<SkillAugmentSO, AugmentSkill> ownedSkills = new Dictionary<SkillAugmentSO, AugmentSkill>();

    public IReadOnlyCollection<SkillAugmentSO> OwnedSkills => ownedSkills.Keys;

    // 스킬 획득/박탈 이벤트
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

    // 처음 획득하면 프리팹을 생성하고, 중복 획득하면 기존 스킬의 스택을 올린다.
    public void AddSkill(SkillAugmentSO so)
    {
        if (so == null || so.skillPrefab == null || IsAtMaxStacks(so))
        {
            return;
        }

        if (ownedSkills.TryGetValue(so, out AugmentSkill ownedSkill))
        {
            ownedSkill.AddStack();
            onSkillsChanged?.Invoke();
            return;
        }

        AugmentSkill skill = Instantiate(so.skillPrefab, transform);
        skill.Apply(gameObject);
        ownedSkills.Add(so, skill);

        onSkillsChanged?.Invoke();
    }

    // 보유 스킬 중 랜덤 1개의 스택을 제거하고, 마지막 스택이면 오브젝트를 Destroy한다.
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

        if (skill == null || !skill.TryRemoveStack())
        {
            ownedSkills.Remove(removed);

            if (skill != null)
            {
                Destroy(skill.gameObject);
            }
        }

        onSkillsChanged?.Invoke();
        return true;
    }
}
