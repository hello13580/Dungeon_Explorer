using System;
using System.Collections.Generic;
using UnityEngine;

public class ManaSystem : MonoBehaviour
{
    [SerializeField] private int maxMana = 100;
    [SerializeField] private int initialMana = 10;  // 전투 시작 시 초기화되는 마나량
    [SerializeField] private int currentMana;

    // -1이면 maxMana의 10%를 기본값으로 사용, 0이면 회복 없음
    [SerializeField] private int baseRegenPerTurn = -1;

    private int bonusRegen = 0;

    // 기간제 마나 재생 버프 — AttackBuffSystem의 keyedBuffs와 동일한 패턴(재사용 시 갱신, 중첩 없음)
    private class RegenBuff
    {
        public int amount;
        public int turnsRemaining;
    }
    private readonly Dictionary<string, RegenBuff> keyedRegenBuffs = new Dictionary<string, RegenBuff>();
    private Unit unit;

    public event EventHandler OnManaChanged;

    private void Awake()
    {
        currentMana = 0;
        unit = GetComponent<Unit>();
    }

    private void Start()
    {
        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
        StageManager.OnStageLoadingStarted += StageManager_OnStageLoadingStarted;
    }

    private void OnDestroy()
    {
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
        StageManager.OnStageLoadingStarted -= StageManager_OnStageLoadingStarted;
    }

    // 스테이지 전환 시 기간제 마나 재생 버프만 해제한다.
    // bonusRegen(스킬 보상으로 받은 영구 마나 재생 증가)은 런 전체에 유지되어야 하므로 건드리지 않는다.
    private void StageManager_OnStageLoadingStarted(object sender, EventArgs e)
    {
        if (keyedRegenBuffs.Count == 0) return;
        keyedRegenBuffs.Clear();
        OnManaChanged?.Invoke(this, EventArgs.Empty);
    }

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        // 이 유닛의 턴이 시작될 때 기간제 재생 버프 지속 턴 차감
        if (unit == null || TurnSystem.Instance.GetTurnUnit() != unit) return;

        var expiredKeys = new List<string>();
        foreach (var kv in keyedRegenBuffs)
        {
            kv.Value.turnsRemaining--;
            if (kv.Value.turnsRemaining <= 0)
                expiredKeys.Add(kv.Key);
        }
        if (expiredKeys.Count == 0) return;

        foreach (string key in expiredKeys)
            keyedRegenBuffs.Remove(key);
        OnManaChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>키가 같은 버프가 이미 있으면 수치·지속 턴을 갱신하고, 없으면 새로 추가한다.</summary>
    public void ApplyOrRefreshRegenBuff(string key, int amount, int duration)
    {
        if (keyedRegenBuffs.TryGetValue(key, out RegenBuff existing))
        {
            existing.amount = amount;
            existing.turnsRemaining = duration;
        }
        else
        {
            keyedRegenBuffs[key] = new RegenBuff { amount = amount, turnsRemaining = duration };
        }
        OnManaChanged?.Invoke(this, EventArgs.Empty);
    }

    private int GetKeyedRegenBonus()
    {
        int total = 0;
        foreach (var buff in keyedRegenBuffs.Values) total += buff.amount;
        return total;
    }

    public bool CanSpendMana(int amount)
    {
        return amount <= 0 || currentMana >= amount;
    }

    public bool SpendMana(int amount)
    {
        if (!CanSpendMana(amount)) return false;
        currentMana -= amount;
        OnManaChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void RegenTurn()
    {
        int baseRegen = baseRegenPerTurn == -1 ? Mathf.Max(1, Mathf.RoundToInt(maxMana * 0.1f)) : baseRegenPerTurn;
        currentMana = Mathf.Min(currentMana + baseRegen + bonusRegen + GetKeyedRegenBonus(), maxMana);
        OnManaChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddBonusRegen(int amount) => bonusRegen += amount;
    public void RemoveBonusRegen(int amount) => bonusRegen = Mathf.Max(0, bonusRegen - amount);
    public int GetBaseRegen() => baseRegenPerTurn == -1 ? Mathf.Max(1, Mathf.RoundToInt(maxMana * 0.1f)) : baseRegenPerTurn;
    public int GetTotalRegen() => GetBaseRegen() + bonusRegen + GetKeyedRegenBonus();

    /// <summary>전투 시작 시 호출. 마나를 initialMana로 초기화한다.</summary>
    public void ResetToInitialMana()
    {
        currentMana = initialMana;
        OnManaChanged?.Invoke(this, EventArgs.Empty);
    }

    public int GetCurrentMana() => currentMana;
    public int GetMaxMana() => maxMana;
    public float GetManaNormalized() => (float)currentMana / maxMana;
}
