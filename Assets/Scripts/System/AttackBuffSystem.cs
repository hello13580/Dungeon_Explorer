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
    private Dictionary<string, AttackBuff> keyedBuffs = new Dictionary<string, AttackBuff>();

    // 오라처럼 "범위 안에 있는 동안만" 유지되는 고정 버프 — 스택되지 않고 켜고 끄는 방식
    private int auraBuff = 0;

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

    /// <summary>키가 같은 버프가 이미 있으면 수치·지속 턴을 갱신하고, 없으면 새로 추가한다.</summary>
    public void ApplyOrRefreshBuff(string key, int amount, int duration)
    {
        if (keyedBuffs.TryGetValue(key, out AttackBuff existing))
        {
            existing.amount = amount;
            existing.turnsRemaining = duration;
        }
        else
        {
            AttackBuff newBuff = new AttackBuff { amount = amount, turnsRemaining = duration };
            keyedBuffs[key] = newBuff;
        }
        OnBuffChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>amount만큼의 공격력 버프를 duration 턴 동안 부여한다.</summary>
    public void ApplyBuff(int amount, int duration)
    {
        activeBuffs.Add(new AttackBuff { amount = amount, turnsRemaining = duration });
        OnBuffChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>오라 범위 안에 있는 동안만 유지되는 고정 버프를 설정한다.</summary>
    public void SetAuraBuff(int amount)
    {
        if (auraBuff == amount) return;
        auraBuff = amount;
        OnBuffChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>오라 고정 버프를 제거한다. 범위 이탈 또는 오라 해제 시 호출.</summary>
    public void ClearAuraBuff()
    {
        if (auraBuff == 0) return;
        auraBuff = 0;
        OnBuffChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>현재 유효한 모든 공격력 버프의 합계를 반환한다.</summary>
    public int GetTotalBonus()
    {
        int total = auraBuff;
        foreach (AttackBuff buff in activeBuffs)
            total += buff.amount;
        foreach (AttackBuff buff in keyedBuffs.Values)
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

        var expiredKeys = new List<string>();
        foreach (var kv in keyedBuffs)
        {
            kv.Value.turnsRemaining--;
            if (kv.Value.turnsRemaining <= 0)
                expiredKeys.Add(kv.Key);
        }
        foreach (string key in expiredKeys)
        {
            keyedBuffs.Remove(key);
            changed = true;
        }

        if (changed)
            OnBuffChanged?.Invoke(this, EventArgs.Empty);
    }
}
