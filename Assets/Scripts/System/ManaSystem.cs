using System;
using UnityEngine;

public class ManaSystem : MonoBehaviour
{
    [SerializeField] private int maxMana = 100;
    [SerializeField] private int currentMana;
    [SerializeField] [Range(0f, 1f)] private float regenPercentPerTurn = 0.1f;

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
        int regenAmount = Mathf.Max(1, Mathf.RoundToInt(maxMana * regenPercentPerTurn));
        currentMana = Mathf.Min(currentMana + regenAmount, maxMana);
        OnManaChanged?.Invoke(this, EventArgs.Empty);
    }

    public int GetCurrentMana() => currentMana;
    public int GetMaxMana() => maxMana;
    public float GetManaNormalized() => (float)currentMana / maxMana;
}
