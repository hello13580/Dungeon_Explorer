using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아드레날린 액션 — 사용 즉시 액션 포인트와 이동 거리를 최대치로 회복한다.
/// </summary>
public class AdrenalineAction : BaseAction
{
    protected override string DefaultActionName() => "아드레날린";
    [Header("Recovery")]
    [SerializeField] private int restoreActionPoints = 1;  // 회복할 액션 포인트 수
    [SerializeField] private bool restoreMovement = true;  // 이동 거리도 함께 회복할지 여부

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }    public override string GetDescription()
    {
        string desc = $"액션 포인트를 {restoreActionPoints} 회복한다.";
        if (restoreMovement) desc += " 이동 거리도 최대치로 회복한다.";
        return desc;
    }


    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        ActionStart(onActionComplete);
        StartCoroutine(AdrenalineRoutine());
    }

    private IEnumerator AdrenalineRoutine()
    {
        // 지정한 액션 포인트만큼 회복
        unit.RestoreActionPoints(restoreActionPoints);

        // 이동 거리 회복 (인스펙터에서 켜고 끌 수 있음)
        if (restoreMovement)
        {
            MoveAction moveAction = unit.GetAction<MoveAction>();
            moveAction?.RestoreMoveDistance();
        }

        yield return new WaitForSeconds(0.3f);

        ActionComplete();
    }

    // 자기 자신 타일만 유효
    public override List<GridPosition> GetValidActionGridPositionList()
    {
        return new List<GridPosition> { unit.GetGridPosition() };
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 40 };
    }
}
