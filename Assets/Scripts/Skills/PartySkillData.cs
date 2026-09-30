using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 파티 전체의 습득한 스킬을 스테이지 간 보존하는 싱글턴.
/// DontDestroyOnLoad로 유지된다.
/// </summary>
public class PartySkillData : MonoBehaviour
{
    public static PartySkillData Instance { get; private set; }

    // unitClassId → 습득한 actionTypeName 집합
    private Dictionary<string, HashSet<string>> learnedSkills = new Dictionary<string, HashSet<string>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool IsLearned(string unitClassId, string actionTypeName)
    {
        return learnedSkills.TryGetValue(unitClassId, out var set) && set.Contains(actionTypeName);
    }

    public void LearnSkill(string unitClassId, string actionTypeName)
    {
        if (!learnedSkills.ContainsKey(unitClassId))
            learnedSkills[unitClassId] = new HashSet<string>();
        learnedSkills[unitClassId].Add(actionTypeName);
    }

    public HashSet<string> GetLearnedSkills(string unitClassId)
    {
        return learnedSkills.TryGetValue(unitClassId, out var set) ? set : new HashSet<string>();
    }

    /// <summary>
    /// SaveSystem이 직렬화할 수 있도록 전체 습득 기록을 반환한다.
    /// Dictionary를 직접 노출하므로 외부에서 수정하지 않도록 주의.
    /// 읽기 전용이 필요하면 IReadOnlyDictionary로 변경할 것.
    /// </summary>
    public Dictionary<string, HashSet<string>> GetAllLearnedSkills() => learnedSkills;

    /// <summary>뉴 게임 시 호출해 모든 습득 기록을 초기화한다.</summary>
    public void ResetAll()
    {
        learnedSkills.Clear();
    }
}
