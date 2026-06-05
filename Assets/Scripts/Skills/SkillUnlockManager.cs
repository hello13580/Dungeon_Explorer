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

    [Tooltip("한 번의 보상에서 제시할 최대 선택지 수")]
    [SerializeField] private int offerCount = 3;

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
        List<SkillUnlockOption> options = BuildOptions();
        if (options.Count == 0)
        {
            // 습득할 스킬이 없으면 바로 완료
            OnSkillUnlockCompleted?.Invoke(this, EventArgs.Empty);
            return;
        }

        OnSkillUnlockStarted?.Invoke(this, options);
    }

    private List<SkillUnlockOption> BuildOptions()
    {
        // 아군 유닛들의 미습득 스킬을 모두 수집
        List<SkillUnlockOption> pool = new List<SkillUnlockOption>();

        foreach (Unit unit in UnitManager.Instance.GetFriendlyUnitList())
        {
            UnitSkillConfig config = unit.GetSkillConfig();
            if (config == null) continue;

            foreach (SkillDefinition skillDef in config.learnableSkills)
            {
                if (PartySkillData.Instance.IsLearned(config.unitClassId, skillDef.actionTypeName)) continue;

                pool.Add(new SkillUnlockOption
                {
                    skillDef = skillDef,
                    unitClassId = config.unitClassId,
                    targetUnit = unit
                });
            }
        }

        // 랜덤 셔플 후 offerCount만큼 반환
        Shuffle(pool);
        int count = Mathf.Min(offerCount, pool.Count);
        return pool.GetRange(0, count);
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
