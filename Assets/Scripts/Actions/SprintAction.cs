using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이번 턴의 이동 거리를 최대치로 회복하는 액션.
/// 둔화(MovementReduce) 상태이상이 걸려있으면 회복량이 그만큼 줄어든다.
/// 속박(Root) 상태이면 이동 거리가 0으로 회복되어 사실상 효과가 없다.
/// </summary>
public class SprintAction : BaseAction
{
    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }

    public override string GetActionName() => "Sprint";
    public override string GetDescription()
    {
        MoveAction move = unit.GetAction<MoveAction>();
        float max = move != null ? move.GetMaxMoveDistance() : 0f;
        return $"이번 턴의 이동 거리를 최대치({max}칸)로 회복한다.";
    }


    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        ActionStart(onActionComplete);
        StartCoroutine(SprintRoutine());
    }

    private IEnumerator SprintRoutine()
    {
        MoveAction moveAction = unit.GetAction<MoveAction>();
        if (moveAction != null)
            // 상태이상 multiplier를 반영해 이동 거리 회복
            moveAction.RestoreMoveDistance();

        yield return new WaitForSeconds(0.2f);

        ActionComplete();
    }

    // 자기 자신 위치만 유효 타일 — 사용 확인용 클릭 대상
    public override List<GridPosition> GetValidActionGridPositionList()
    {
        return new List<GridPosition> { unit.GetGridPosition() };
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        MoveAction moveAction = unit.GetAction<MoveAction>();
        if (moveAction == null) return null;

        // 이동 거리가 이미 최대면 굳이 쓸 이유 없음
        float remaining = moveAction.GetLeftMoveDistance();
        float max = moveAction.GetMaxMoveDistance();
        int gain = Mathf.RoundToInt((max - remaining) * 10f);
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = gain };
    }
}
