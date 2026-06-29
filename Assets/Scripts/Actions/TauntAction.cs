using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 주위 적들을 도발하여 일정 턴 동안 자신만 타겟으로 삼도록 강제하는 액션.
/// </summary>
public class TauntAction : BaseAction
{
    protected override string DefaultActionName() => "도발";
    [Header("Taunt Settings")]
    [SerializeField] private int tauntRange = 4;    // 도발 적용 반경 (칸)
    [SerializeField] private int tauntDuration = 2; // 지속 턴 수
    [SerializeField] private float tauntDelay = 0.3f;

    public event EventHandler OnTauntStarted;
    public event EventHandler OnTauntEnded;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }    public override string GetDescription() =>
        $"반경 {tauntRange}칸 내 모든 적을 도발해 {tauntDuration}턴 동안 자신만 공격하도록 강제한다.";


    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        ActionStart(onActionComplete);
        StartCoroutine(TauntRoutine());
    }

    private IEnumerator TauntRoutine()
    {
        OnTauntStarted?.Invoke(this, EventArgs.Empty);

        yield return new WaitForSeconds(tauntDelay);

        TauntManager.Instance.ApplyTaunt(unit, tauntDuration);

        OnTauntEnded?.Invoke(this, EventArgs.Empty);
        ActionComplete();
    }

    /// <summary>
    /// 도발은 범위 내 적이 있을 때만 유효.
    /// 단, 시전 위치는 자기 자신 위치 하나만 (자신에게 시전하는 액션).
    /// </summary>
    /// 노란색: 도발 영향을 받는 적 위치
    public override List<GridPosition> GetValidActionGridPositionList()
    {
        List<GridPosition> enemyList = new List<GridPosition>();
        foreach (GridPosition pos in GetActionRangeGridPositionList())
        {
            if (!LevelGrid.Instance.IsGridPositionOccupied(pos)) continue;
            Unit target = LevelGrid.Instance.GetUnitListAtGridPosition(pos)[0];
            if (TeamHelper.IsHostile(unit.GetTeamType(), target.GetTeamType()) && !target.IsStealthed())
                enemyList.Add(pos);
        }
        return enemyList;
    }

    /// 초록색: 자기 자신 위치 (클릭 지점)
    public override List<GridPosition> GetSecondaryHighlightGridPositionList()
    {
        if (GetValidActionGridPositionList().Count == 0)
            return new List<GridPosition>();
        return new List<GridPosition> { unit.GetGridPosition() };
    }

    public override GridSystemVisual.GridVisualType GetSecondaryHighlightColor()
        => GridSystemVisual.GridVisualType.Green;

    public override bool IsValidActionGridPosition(GridPosition gridPosition)
    {
        // 실제 클릭은 자기 자신 위치만 허용 (범위 내 적이 있을 때)
        if (gridPosition != unit.GetGridPosition()) return false;
        return GetValidActionGridPositionList().Count > 0;
    }

    /// <summary>도발 범위 표시용 — 주위 원형 범위 전체</summary>
    public override List<GridPosition> GetActionRangeGridPositionList()
    {
        GridPosition unitPos = unit.GetGridPosition();
        List<GridPosition> rangeList = new List<GridPosition>();

        for (int x = -tauntRange; x <= tauntRange; x++)
        {
            for (int z = -tauntRange; z <= tauntRange; z++)
            {
                if (x == 0 && z == 0) continue;
                if (Mathf.Sqrt(x * x + z * z) > tauntRange) continue;

                GridPosition testPos = new GridPosition(unitPos.x + x, unitPos.z + z, unitPos.floor);
                if (LevelGrid.Instance.IsValidGridPosition(testPos))
                    rangeList.Add(testPos);
            }
        }
        return rangeList;
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        // 적은 도발 액션을 사용하지 않음
        return null;
    }
}
