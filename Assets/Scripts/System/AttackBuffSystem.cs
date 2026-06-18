using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛에 일시적인 공격력 버프를 부여하고 턴 경과에 따라 만료시킨다.
/// BarrierSystem과 동일한 구조 — 버프는 스택 형태로 쌓이며 각각 지속 턴이 독립적이다.
/// </summary>
public class AttackBuffSystem : MonoBehaviour
{
    public event EventHandler OnBuffChanged; // 버프 수치가 바뀔 때 — UI 갱신용

    private class AttackBuff
    {
        public int amount;
        public int turnsRemaining;
    }

    private List<AttackBuff> activeBuffs = new List<AttackBuff>();
    private Unit unit;

    private void Awake()
    {
        unit = GetComponent<Unit>();
    }

    private void Start()
    {
        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
    }

    private void OnDestroy()
    {
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
    }

    /// <summary>amount만큼의 공격력 버프를 duration 턴 동안 부여한다.</summary>
    public void ApplyBuff(int amount, int duration)
    {
        activeBuffs.Add(new AttackBuff { amount = amount, turnsRemaining = duration });
        OnBuffChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>현재 유효한 모든 공격력 버프의 합계를 반환한다.</summary>
    public int GetTotalBonus()
    {
        int total = 0;
        foreach (AttackBuff buff in activeBuffs)
            total += buff.amount;
        return total;
    }

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        // 이 유닛의 턴이 시작될 때 버프 지속 턴 차감
        if (TurnSystem.Instance.GetTurnUnit() != unit) return;

        bool changed = false;
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            activeBuffs[i].turnsRemaining--;
            if (activeBuffs[i].turnsRemaining <= 0)
            {
                activeBuffs.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
            OnBuffChanged?.Invoke(this, EventArgs.Empty);
    }
}
