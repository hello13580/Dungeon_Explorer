using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 클리어 후 스킬 보상 흐름을 관리한다.
/// DontDestroyOnLoad로 유지된다.
/// </summary>
public class SkillUnlockManager : MonoBehaviour
{
    public static SkillUnlockManager Instance { get; private set; }


    /// <summary>스킬 선택 UI에 넘겨줄 선택지 하나.</summary>
    public class SkillUnlockOption
    {
        public SkillDefinition skillDef;
        public string unitClassId;
        public Unit targetUnit; // 현재 살아있는 유닛 인스턴스
    }

    public static event EventHandler<List<SkillUnlockOption>> OnSkillUnlockStarted;
    public static event EventHandler OnSkillUnlockCompleted;

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

    private void OnEnable()
    {
        UnitManager.OnStageClear += UnitManager_OnStageClear;
    }

    private void OnDisable()
    {
        UnitManager.OnStageClear -= UnitManager_OnStageClear;
    }

    private void UnitManager_OnStageClear(object sender, EventArgs e)
    {
        TriggerSkillUnlock();
    }

    private void TriggerSkillUnlock()
    {
        // 선택지가 없어도 보상 패널은 항상 표시한다 (골드 확인 등 용도)
        List<SkillUnlockOption> options = BuildOptions();
        OnSkillUnlockStarted?.Invoke(this, options);
    }

    /// <summary>스킬을 선택하지 않고 보상 패널을 닫을 때 호출.</summary>
    public void SkipUnlock()
    {
        OnSkillUnlockCompleted?.Invoke(this, EventArgs.Empty);
    }

    private List<SkillUnlockOption> BuildOptions()
    {
        List<SkillUnlockOption> options = new List<SkillUnlockOption>();
        foreach (Unit unit in UnitManager.Instance.GetFriendlyUnitList())
        {
            UnitSkillConfig config = unit.GetSkillConfig();
            if (config == null) continue;

            // 이 유닛 클래스의 미습득 스킬 목록
            List<SkillDefinition> unlearnedSkills = new List<SkillDefinition>();
            foreach (SkillDefinition skillDef in config.learnableSkills)
            {
                if (!PartySkillData.Instance.IsLearned(config.unitClassId, skillDef.actionTypeName))
                    unlearnedSkills.Add(skillDef);
            }

            if (unlearnedSkills.Count == 0) continue;

            // 미습득 스킬 중 랜덤으로 1개 선택
            SkillDefinition picked = unlearnedSkills[UnityEngine.Random.Range(0, unlearnedSkills.Count)];
            options.Add(new SkillUnlockOption
            {
                skillDef = picked,
                unitClassId = config.unitClassId,
                targetUnit = unit
            });
        }

        return options;
    }

    /// <summary>UI에서 플레이어가 스킬을 선택했을 때 호출.</summary>
    public void ConfirmUnlock(SkillUnlockOption option)
    {
        if (option == null || option.skillDef == null) return;

        // 1. PartySkillData에 영구 저장
        PartySkillData.Instance.LearnSkill(option.unitClassId, option.skillDef.actionTypeName);

        // 2. 현재 살아있는 유닛에 즉시 적용
        if (option.targetUnit != null)
            option.targetUnit.UnlockSkillByTypeName(option.skillDef.actionTypeName);

        OnSkillUnlockCompleted?.Invoke(this, EventArgs.Empty);
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
