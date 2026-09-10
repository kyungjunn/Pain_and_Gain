using UnityEngine;
using System;
using System.Collections.Generic;

public enum SkillAcquireStatus
{
    Invalid,
    AlreadyMax,
    Instantiated,
    Stacked
}

// 플레이어가 보유한 스킬 증강 관리.
public class PlayerAugments : MonoBehaviour
{
    private readonly Dictionary<SkillAugmentSO, AugmentSkill> ownedSkills = new Dictionary<SkillAugmentSO, AugmentSkill>();

    public IReadOnlyCollection<SkillAugmentSO> OwnedSkills => ownedSkills.Keys;
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

    public SkillAcquireStatus TryAcquireLoadedSkill(SkillAugmentSO so, GameObject prefab)
    {
        if (so == null)
        {
            return SkillAcquireStatus.Invalid;
        }

        if (IsAtMaxStacks(so))
        {
            return SkillAcquireStatus.AlreadyMax;
        }

        if (ownedSkills.TryGetValue(so, out AugmentSkill ownedSkill) && ownedSkill != null)
        {
            ownedSkill.AddStack();
            onSkillsChanged?.Invoke();
            return SkillAcquireStatus.Stacked;
        }

        if (prefab == null || prefab.GetComponent<AugmentSkill>() == null)
        {
            return SkillAcquireStatus.Invalid;
        }

        AugmentSkill skill = Instantiate(prefab, transform).GetComponent<AugmentSkill>();
        if (skill == null)
        {
            return SkillAcquireStatus.Invalid;
        }

        skill.Apply(gameObject);
        ownedSkills[so] = skill;
        onSkillsChanged?.Invoke();
        return SkillAcquireStatus.Instantiated;
    }

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
                skill.Release();
                Destroy(skill.gameObject);
            }
        }

        onSkillsChanged?.Invoke();
        return true;
    }

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

        AugmentResourceLoader host = AugmentResourceLoader.Instance;
        if (host != null)
        {
            host.NotifyPlayerUnavailable(gameObject);
        }
    }
}
