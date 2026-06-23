using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelfHealAction : BaseAction
{
    [SerializeField] private int healAmount = 30;

    public static event EventHandler<OnHealEventArgs> OnAnySelfHeal;
    public event EventHandler<OnHealEventArgs> OnSelfHeal;

    public class OnHealEventArgs : EventArgs
    {
        public Unit targetUnit;
    }

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }

    public override string GetActionName() => "SelfHeal";
    public override string GetDescription() => $"자신의 체력을 {healAmount} 회복한다.";

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        return new List<GridPosition> { unit.GetGridPosition() };
    }

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        ActionStart(onActionComplete);
        StartCoroutine(SelfHealRoutine());
    }

    private IEnumerator SelfHealRoutine()
    {
        yield return new WaitForSeconds(0.1f);

        unit.Heal(healAmount);
        OnSelfHeal?.Invoke(this, new OnHealEventArgs { targetUnit = unit });
        OnAnySelfHeal?.Invoke(this, new OnHealEventArgs { targetUnit = unit });

        yield return new WaitForSeconds(0.5f);

        ActionComplete();
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        int missingHealthPercent = 100 - Mathf.RoundToInt(unit.GetHealthNormalized() * 100f);
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 100 + missingHealthPercent * 2 };
    }

    public int GetHealAmount() => healAmount;
}
