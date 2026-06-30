using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사용하면 다음 세 번의 BowAction 발사에 액션 포인트 소모량을 1씩 줄여준다.
/// </summary>
public class RapidFireAction : BaseAction
{
    protected override string DefaultActionName() => "연발 사격";

    [SerializeField] private int chargeCount = 3; // 할인이 적용될 다음 활 공격 횟수

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }

    public override string GetDescription() =>
        $"다음 {chargeCount}번의 활 공격 액션 포인트 소모량을 1 줄인다.";

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        ActionStart(onActionComplete);

        BowAction bowAction = unit.GetAction<BowAction>();
        bowAction?.AddDiscountedShots(chargeCount);

        StartCoroutine(Routine());
    }

    private IEnumerator Routine()
    {
        yield return new WaitForSeconds(0.3f);
        ActionComplete();
    }

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        // BowAction이 없는 유닛은 사용할 의미가 없음
        if (unit.GetAction<BowAction>() == null) return new List<GridPosition>();
        return new List<GridPosition> { unit.GetGridPosition() };
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        if (unit.GetAction<BowAction>() == null) return null;
        // 활 공격(1000+)보다는 낮지만 자기강화 버프류와 비슷한 우선순위로 선딜 사용
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 70 };
    }
}
