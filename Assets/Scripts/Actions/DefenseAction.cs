using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자신에게 방어막을 부여하는 액션.
/// 방어막 수치 = barrierAmount + 시전자의 defensePower 스탯.
/// </summary>
public class DefenseAction : BaseAction
{
    protected override string DefaultActionName() => "방어";
    [SerializeField] private int barrierAmount = 20;   // 기본 방어막 수치
    [SerializeField] private int barrierDuration = 2;  // 방어막 지속 턴

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }    public override string GetDescription() =>
        $"자신에게 {barrierAmount + unit.GetDefensePower()} 방어막을 부여한다. ({barrierDuration}턴 지속)";


    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        ActionStart(onActionComplete);
        StartCoroutine(DefenseRoutine());
    }

    private IEnumerator DefenseRoutine()
    {
        yield return new WaitForSeconds(0.1f);

        BarrierSystem barrierSystem = unit.GetComponent<BarrierSystem>();
        if (barrierSystem != null)
            // 고정 방어막 수치 + 시전자 방어력 스탯
            barrierSystem.ApplyBarrier(barrierAmount + unit.GetDefensePower(), barrierDuration);

        yield return new WaitForSeconds(0.3f);

        ActionComplete();
    }

    // 자신의 위치만 유효 타일로 반환 — 자기 자신에게만 사용 가능
    public override List<GridPosition> GetValidActionGridPositionList()
    {
        return new List<GridPosition> { unit.GetGridPosition() };
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        // 체력이 낮을수록 방어 우선순위 높임
        int missingHealthPercent = 100 - Mathf.RoundToInt(unit.GetHealthNormalized() * 100f);
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 100 + missingHealthPercent };
    }
}
