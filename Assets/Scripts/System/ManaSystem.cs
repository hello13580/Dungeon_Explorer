using System;
using UnityEngine;

public class ManaSystem : MonoBehaviour
{
    [SerializeField] private int maxMana = 100;
    [SerializeField] private int initialMana = 10;  // 전투 시작 시 초기화되는 마나량
    [SerializeField] private int currentMana;

    // -1이면 maxMana의 10%를 기본값으로 사용, 0이면 회복 없음
    [SerializeField] private int baseRegenPerTurn = -1;

    private int bonusRegen = 0;

    public event EventHandler OnManaChanged;

    private void Awake()
    {
        currentMana = 0;
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
        currentMana = Mathf.Min(currentMana + baseRegen + bonusRegen, maxMana);
        OnManaChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddBonusRegen(int amount) => bonusRegen += amount;
    public void RemoveBonusRegen(int amount) => bonusRegen = Mathf.Max(0, bonusRegen - amount);
    public int GetBaseRegen() => baseRegenPerTurn == -1 ? Mathf.Max(1, Mathf.RoundToInt(maxMana * 0.1f)) : baseRegenPerTurn;
    public int GetTotalRegen() => GetBaseRegen() + bonusRegen;

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
