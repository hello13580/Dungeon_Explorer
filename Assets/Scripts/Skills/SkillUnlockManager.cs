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

    /// <summary>스킬 선택 UI에 넘겨줄 선택지 하나.</summary>
    public class SkillUnlockOption
    {
        public SkillDefinition skillDef;
        public string unitClassId;
        public Unit targetUnit;
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

    /// <summary>확정 버튼 클릭 시 호출. 스킬을 습득하지만 패널은 닫지 않는다.</summary>
    public void ConfirmUnlock(SkillUnlockOption option)
    {
        if (option == null || option.skillDef == null) return;

        PartySkillData.Instance.LearnSkill(option.unitClassId, option.skillDef.actionTypeName);

        if (option.targetUnit != null)
            option.targetUnit.UnlockSkillByTypeName(option.skillDef.actionTypeName);
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

            // 미습득 스킬을 섞어서 최대 optionsPerClass개 선택
            Shuffle(unlearnedSkills);
            List<SkillUnlockOption> classOptions = new List<SkillUnlockOption>();
            int count = Mathf.Min(optionsPerClass, unlearnedSkills.Count);
            for (int i = 0; i < count; i++)
            {
                classOptions.Add(new SkillUnlockOption
                {
                    skillDef    = unlearnedSkills[i],
                    unitClassId = config.unitClassId,
                    targetUnit  = unit
                });
            }
            result.Add(classOptions);
        }

        return result;
    }

    /// <summary>어셈블리 전체에서 typeName에 해당하는 Type을 찾는다.</summary>
    private static System.Type FindActionType(string typeName)
    {
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
