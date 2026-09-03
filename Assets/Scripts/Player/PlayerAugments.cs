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

    // 스킬 프리팹을 플레이어 밑에 생성하고 등록
    public void AddSkill(SkillAugmentSO so)
    {
        if (so == null || so.skillPrefab == null || ownedSkills.ContainsKey(so))
        {
            return;
        }

        AugmentSkill skill = Instantiate(so.skillPrefab, transform);
        skill.Apply(gameObject);
        ownedSkills.Add(so, skill);

        onSkillsChanged?.Invoke();
    }

    // 보유 스킬 중 랜덤 1개를 Destroy. 원복은 AugmentSkill.OnDestroy가 담당한다.
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
        ownedSkills.Remove(removed);

        if (skill != null)
        {
            Destroy(skill.gameObject);
        }

        onSkillsChanged?.Invoke();
        return true;
    }
}
