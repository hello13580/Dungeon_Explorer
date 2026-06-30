using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 클리어 후 스킬 보상 흐름을 관리한다.
/// 아군 직업마다 보상을 1번씩 순서대로 보여주고, 전부 완료되면 OnSkillUnlockCompleted를 발생시킨다.
/// DontDestroyOnLoad로 유지된다.
/// </summary>
public class SkillUnlockManager : MonoBehaviour
{
    public static SkillUnlockManager Instance { get; private set; }

    [SerializeField] private int optionsPerClass = 3; // 직업당 보여줄 선택지 수

    /// <summary>영구 스탯/토큰 보상 종류.</summary>
    public enum StatBoostType
    {
        Attack,
        Speed,
        Defense,
        Token,
        ManaRegen,
    }

    /// <summary>스킬 선택 UI에 넘겨줄 선택지 하나.</summary>
    public class SkillUnlockOption
    {
        public SkillDefinition skillDef;
        public string unitClassId;
        public Unit targetUnit;

        /// <summary>true면 스킬 습득이 아니라 영구 스탯 증가/토큰 보상이다.</summary>
        public bool isStatBoost;
        public StatBoostType statBoostType;
    }

    /// <summary>직업 보상 차례가 시작될 때 발생. 해당 직업의 선택지 목록을 전달한다.</summary>
    public static event EventHandler<List<SkillUnlockOption>> OnSkillUnlockStarted;
    /// <summary>모든 직업의 보상이 끝났을 때 발생.</summary>
    public static event EventHandler OnSkillUnlockCompleted;

    // 직업별 선택지 묶음을 순서대로 처리하기 위한 큐
    // 각 항목은 한 직업의 선택지 목록 (최대 optionsPerClass개)
    private Queue<List<SkillUnlockOption>> optionQueue = new Queue<List<SkillUnlockOption>>();

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
        // 스테이지 로딩 중 발생하는 OnStageClear는 무시한다.
        // 유닛 리스트 재구성 과정에서 조건이 잠깐 맞아 spurious하게 발생할 수 있다.
        if (StageManager.Instance != null && StageManager.Instance.IsLoading) return;

        TriggerSkillUnlock();
    }

    private void TriggerSkillUnlock()
    {
        optionQueue.Clear();
        foreach (List<SkillUnlockOption> classOptions in BuildOptions())
            optionQueue.Enqueue(classOptions);

        // 배울 스킬이 하나도 없으면 보상 화면 자체를 열지 않는다
        if (optionQueue.Count == 0) return;

        ShowNextOption();
    }

    /// <summary>
    /// 큐에서 다음 직업 보상 묶음을 꺼내 OnSkillUnlockStarted를 발생시킨다.
    /// 큐가 비면 모든 보상이 끝난 것이므로 OnSkillUnlockCompleted를 발생시킨다.
    /// </summary>
    private void ShowNextOption()
    {
        if (optionQueue.Count == 0)
        {
            OnSkillUnlockCompleted?.Invoke(this, EventArgs.Empty);
            return;
        }

        List<SkillUnlockOption> next = optionQueue.Dequeue();
        OnSkillUnlockStarted?.Invoke(this, next);
    }

    /// <summary>컨티뉴 버튼 클릭 시 호출. 다음 직업 보상으로 이동한다.</summary>
    public void SkipUnlock()
    {
        ShowNextOption();
    }

    /// <summary>확정 버튼 클릭 시 호출. 스킬을 습득하거나 스탯 보상을 적용한다. 패널은 닫지 않는다.</summary>
    public void ConfirmUnlock(SkillUnlockOption option)
    {
        if (option == null) return;

        if (option.isStatBoost)
        {
            ApplyStatBoost(option);
            return;
        }

        if (option.skillDef == null) return;

        PartySkillData.Instance.LearnSkill(option.unitClassId, option.skillDef.actionTypeName);

        if (option.targetUnit != null)
            option.targetUnit.UnlockSkillByTypeName(option.skillDef.actionTypeName);
    }

    private void ApplyStatBoost(SkillUnlockOption option)
    {
        switch (option.statBoostType)
        {
            case StatBoostType.Attack:
                option.targetUnit?.AddPermanentAttackPower(1);
                break;
            case StatBoostType.Speed:
                option.targetUnit?.AddPermanentSpeed(1f);
                break;
            case StatBoostType.Defense:
                option.targetUnit?.AddPermanentDefensePower(1);
                break;
            case StatBoostType.Token:
                SkillEnhancementTokenManager.Instance.AddTokens(1);
                break;
            case StatBoostType.ManaRegen:
                option.targetUnit?.GetManaSystem()?.AddBonusRegen(1);
                break;
        }
    }

    /// <summary>
    /// 직업별 선택지 묶음 목록을 만든다.
    /// 각 직업마다 미습득 스킬 중 최대 optionsPerClass개를 랜덤으로 골라 묶음으로 반환한다.
    /// </summary>
    private List<List<SkillUnlockOption>> BuildOptions()
    {
        List<List<SkillUnlockOption>> result = new List<List<SkillUnlockOption>>();
        HashSet<string> processedClasses = new HashSet<string>();

        foreach (Unit unit in UnitManager.Instance.GetFriendlyUnitList())
        {
            UnitSkillConfig config = unit.GetSkillConfig();
            if (config == null) continue;
            if (processedClasses.Contains(config.unitClassId)) continue;
            processedClasses.Add(config.unitClassId);

            // 이 직업의 미습득 스킬 목록
            // PartySkillData에 없는 것 + 유닛에 이미 활성화된 컴포넌트가 아닌 것만 포함
            List<SkillDefinition> unlearnedSkills = new List<SkillDefinition>();
            foreach (SkillDefinition skillDef in config.learnableSkills)
            {
                // 보상으로 이미 습득한 스킬 제외
                if (PartySkillData.Instance.IsLearned(config.unitClassId, skillDef.actionTypeName))
                    continue;

                // 처음부터 활성화된 스킬(기본 스킬) 제외
                System.Type actionType = FindActionType(skillDef.actionTypeName);
                if (actionType != null)
                {
                    BaseAction existing = unit.GetComponent(actionType) as BaseAction;
                    if (existing != null && existing.enabled) continue;
                }

                unlearnedSkills.Add(skillDef);
            }

            // 배울 스킬이 없으면 이 직업은 보상 큐에 넣지 않는다
            if (unlearnedSkills.Count == 0) continue;

            // 선택지 중 하나는 항상 영구 스탯/토큰 보상으로 고정 배정하고,
            // 나머지(최대 optionsPerClass - 1개)는 미습득 스킬로 채운다.
            Shuffle(unlearnedSkills);
            List<SkillUnlockOption> classOptions = new List<SkillUnlockOption>();
            int skillSlotCount = Mathf.Min(optionsPerClass - 1, unlearnedSkills.Count);
            for (int i = 0; i < skillSlotCount; i++)
            {
                classOptions.Add(new SkillUnlockOption
                {
                    skillDef    = unlearnedSkills[i],
                    unitClassId = config.unitClassId,
                    targetUnit  = unit
                });
            }
            classOptions.Add(BuildRandomStatBoostOption(config.unitClassId, unit));

            // 스탯 보상 슬롯이 항상 마지막에 있지 않도록 섞는다
            Shuffle(classOptions);

            result.Add(classOptions);
        }

        return result;
    }

    /// <summary>
    /// 공격력/속도/방어력/스킬 강화 토큰 중 하나를 무작위로 골라 스탯 보상 선택지를 만든다.
    /// skillDef는 표시용으로 즉석에서 만든 SkillDefinition이며, actionTypeName은 비워둔다
    /// (실제 스킬이 아니므로 PartySkillData에 등록되지 않는다).
    /// </summary>
    private SkillUnlockOption BuildRandomStatBoostOption(string unitClassId, Unit unit)
    {
        // 마나 시스템이 없는 유닛(예: 마나를 안 쓰는 직업)에게는 ManaRegen 후보를 제외한다.
        List<StatBoostType> candidates = new List<StatBoostType>
        {
            StatBoostType.Attack,
            StatBoostType.Speed,
            StatBoostType.Defense,
            StatBoostType.Token,
        };
        if (unit.GetManaSystem() != null)
            candidates.Add(StatBoostType.ManaRegen);

        StatBoostType type = candidates[UnityEngine.Random.Range(0, candidates.Count)];

        string name, desc;
        switch (type)
        {
            case StatBoostType.Attack:
                name = "공격력 증가";
                desc = "공격력이 영구히 1 증가한다.";
                break;
            case StatBoostType.Speed:
                name = "속도 증가";
                desc = "속도가 영구히 1 증가한다.";
                break;
            case StatBoostType.Defense:
                name = "방어력 증가";
                desc = "방어력이 영구히 1 증가한다.";
                break;
            case StatBoostType.ManaRegen:
                name = "마나 재생 증가";
                desc = "턴마다 회복하는 마나가 영구히 1 증가한다.";
                break;
            default:
                name = "스킬 강화 토큰 획득";
                desc = "스킬 강화 토큰을 1개 획득한다.";
                break;
        }

        SkillDefinition displayDef = ScriptableObject.CreateInstance<SkillDefinition>();
        displayDef.skillName = name;
        displayDef.description = desc;
        // 실제 액션이 아니므로 빈 문자열로 둔다 (null이면 FindActionType의 Assembly.GetType(null)이 예외를 던짐)
        displayDef.actionTypeName = "";

        return new SkillUnlockOption
        {
            skillDef    = displayDef,
            unitClassId = unitClassId,
            targetUnit  = unit,
            isStatBoost = true,
            statBoostType = type,
        };
    }

    /// <summary>어셈블리 전체에서 typeName에 해당하는 Type을 찾는다.</summary>
    private static System.Type FindActionType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return null;
        foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type type = assembly.GetType(typeName);
            if (type != null) return type;
        }
        return null;
    }

    /// <summary>Fisher-Yates 셔플로 리스트를 무작위로 섞는다.</summary>
    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
