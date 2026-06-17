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

    /// <summary>스킬 선택 UI에 넘겨줄 선택지 하나.</summary>
    public class SkillUnlockOption
    {
        public SkillDefinition skillDef;
        public string unitClassId;
        public Unit targetUnit; // 현재 살아있는 유닛 인스턴스
    }

    /// <summary>직업 보상 차례가 시작될 때 발생. 해당 직업의 선택지(없으면 빈 리스트)를 전달한다.</summary>
    public static event EventHandler<List<SkillUnlockOption>> OnSkillUnlockStarted;
    /// <summary>모든 직업의 보상이 끝났을 때 발생.</summary>
    public static event EventHandler OnSkillUnlockCompleted;

    // 직업별 보상을 순서대로 처리하기 위한 큐
    private Queue<SkillUnlockOption> optionQueue = new Queue<SkillUnlockOption>();

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
        // 아군 직업마다 보상 선택지를 큐에 쌓은 뒤 첫 번째 직업부터 순서대로 표시한다
        optionQueue.Clear();
        foreach (SkillUnlockOption option in BuildOptions())
            optionQueue.Enqueue(option);

        ShowNextOption();
    }

    /// <summary>
    /// 큐에서 다음 직업 보상을 꺼내 OnSkillUnlockStarted를 발생시킨다.
    /// 큐가 비면 모든 보상이 끝난 것이므로 OnSkillUnlockCompleted를 발생시킨다.
    /// SkipUnlock()에서 호출해 다음 직업으로 넘어간다.
    /// </summary>
    private void ShowNextOption()
    {
        if (optionQueue.Count == 0)
        {
            // 모든 직업 보상 완료
            OnSkillUnlockCompleted?.Invoke(this, EventArgs.Empty);
            return;
        }

        SkillUnlockOption next = optionQueue.Dequeue();
        // 단일 선택지를 리스트로 감싸서 기존 UI 이벤트 시그니처를 유지한다
        OnSkillUnlockStarted?.Invoke(this, new List<SkillUnlockOption> { next });
    }

    /// <summary>
    /// 컨티뉴 버튼 클릭 시 호출.
    /// 현재 직업 보상을 넘기고 다음 직업 보상으로 이동한다.
    /// 마지막 직업이면 패널을 닫는다.
    /// </summary>
    public void SkipUnlock()
    {
        ShowNextOption();
    }

    /// <summary>
    /// 확정 버튼 클릭 시 호출. 스킬을 습득하지만 패널은 닫지 않는다.
    /// 패널은 컨티뉴 버튼(SkipUnlock)으로만 다음 단계로 넘어간다.
    /// </summary>
    public void ConfirmUnlock(SkillUnlockOption option)
    {
        if (option == null || option.skillDef == null) return;

        // 1. PartySkillData에 영구 저장
        PartySkillData.Instance.LearnSkill(option.unitClassId, option.skillDef.actionTypeName);

        // 2. 현재 살아있는 유닛에 즉시 적용
        if (option.targetUnit != null)
            option.targetUnit.UnlockSkillByTypeName(option.skillDef.actionTypeName);
    }

    private List<SkillUnlockOption> BuildOptions()
    {
        List<SkillUnlockOption> options = new List<SkillUnlockOption>();

        // 이미 처리한 직업 ID는 건너뛴다 (같은 직업 유닛이 여러 명일 때 중복 방지)
        HashSet<string> processedClasses = new HashSet<string>();

        foreach (Unit unit in UnitManager.Instance.GetFriendlyUnitList())
        {
            UnitSkillConfig config = unit.GetSkillConfig();
            if (config == null) continue;
            if (processedClasses.Contains(config.unitClassId)) continue;
            processedClasses.Add(config.unitClassId);

            // 이 직업의 미습득 스킬 목록
            List<SkillDefinition> unlearnedSkills = new List<SkillDefinition>();
            foreach (SkillDefinition skillDef in config.learnableSkills)
            {
                if (!PartySkillData.Instance.IsLearned(config.unitClassId, skillDef.actionTypeName))
                    unlearnedSkills.Add(skillDef);
            }

            // 배울 스킬이 없는 직업도 큐에 넣어 "습득 가능한 스킬 없음" 화면을 보여준다
            if (unlearnedSkills.Count == 0)
            {
                options.Add(new SkillUnlockOption
                {
                    skillDef = null,
                    unitClassId = config.unitClassId,
                    targetUnit = unit
                });
                continue;
            }

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
}
