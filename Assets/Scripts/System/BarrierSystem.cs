using System;
using System.Collections.Generic;
using UnityEngine;

public class BarrierSystem : MonoBehaviour
{
    private class BarrierEntry
    {
        public int amount;
        public int turnsRemaining;

        public BarrierEntry(int amount, int turns)
        {
            this.amount = amount;
            this.turnsRemaining = turns;
        }
    }

    private List<BarrierEntry> barriers = new List<BarrierEntry>();

    public event EventHandler OnBarrierChanged;
    public event EventHandler OnBarrierExpired;

    private void Start()
    {
        TurnSystem.Instance.OnTurnChanged += OnTurnChanged;
    }

    private void OnDestroy()
    {
        TurnSystem.Instance.OnTurnChanged -= OnTurnChanged;
    }

    private void OnTurnChanged(object sender, EventArgs e)
    {
        // 이 유닛의 턴이 시작될 때 모든 배리어 턴 감소
        if (TurnSystem.Instance.GetTurnUnit() != GetComponent<Unit>()) return;

        bool anyExpired = false;
        for (int i = barriers.Count - 1; i >= 0; i--)
        {
            barriers[i].turnsRemaining--;
            if (barriers[i].turnsRemaining <= 0)
            {
                barriers.RemoveAt(i);
                anyExpired = true;
            }
        }

        if (anyExpired) OnBarrierExpired?.Invoke(this, EventArgs.Empty);
        OnBarrierChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyBarrier(int amount, int turns)
    {
        barriers.Add(new BarrierEntry(amount, turns));
        // 짧은 턴 순으로 정렬 (데미지 흡수 시 짧은 것부터 소진)
        barriers.Sort((a, b) => a.turnsRemaining.CompareTo(b.turnsRemaining));
        OnBarrierChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 데미지를 배리어로 흡수한다. 흡수되고 남은 데미지를 반환한다.
    /// </summary>
    public int AbsorbDamage(int damageAmount)
    {
        for (int i = 0; i < barriers.Count && damageAmount > 0; i++)
        {
            int absorbed = Mathf.Min(barriers[i].amount, damageAmount);
            barriers[i].amount -= absorbed;
            damageAmount -= absorbed;
        }

        barriers.RemoveAll(b => b.amount <= 0);
        OnBarrierChanged?.Invoke(this, EventArgs.Empty);
        return damageAmount;
    }

    public bool HasBarrier() => barriers.Count > 0;

    public int GetTotalBarrierAmount()
    {
        int total = 0;
        foreach (var b in barriers) total += b.amount;
        return total;
    }
}
