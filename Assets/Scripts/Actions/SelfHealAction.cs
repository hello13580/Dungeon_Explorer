using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SelfHealAction : BaseAction
{
    [SerializeField] private int healAmount = 30;

    [Header("적 AI 설정")]
    [SerializeField] private float healthThreshold = 0.5f;  // 이 체력 % 이하일 때만 고려 (0~1)
    [SerializeField] private float castChance = 0.6f;       // 시전 확률 (0~1)

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
        // 체력이 threshold 초과면 힐 고려 안 함 (null = 이 액션 선택 불가)
        if (unit.GetHealthNormalized() > healthThreshold)
            return null;

        // 확률 체크 — 실패 시 이 턴엔 힐 안 함
        if (UnityEngine.Random.value > castChance)
            return null;

        // 체력이 낮을수록 더 높은 우선순위
        int missingHealthPercent = 100 - Mathf.RoundToInt(unit.GetHealthNormalized() * 100f);
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 200 + missingHealthPercent * 2 };
    }

    public int GetHealAmount() => healAmount;
}
