using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자기 자신에게 공격력 버프를 거는 액션.
/// </summary>
public class SelfAttackBuffAction : BaseAction
{
    protected override string DefaultActionName() => "공격력 강화";

    [Header("버프")]
    [SerializeField] private int attackBonus = 10;
    [SerializeField] private int duration = 3;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }

    public override string GetDescription() => $"공격력을 {attackBonus} 만큼 {duration}턴 동안 증가시킨다.";

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        ActionStart(onActionComplete);
        StartCoroutine(BuffRoutine());
    }

    private IEnumerator BuffRoutine()
    {
        unit.GetComponent<AttackBuffSystem>()?.ApplyOrRefreshBuff("SelfAttackBuff", attackBonus, duration);
        yield return new WaitForSeconds(0.3f);
        ActionComplete();
    }

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        return new List<GridPosition> { unit.GetGridPosition() };
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        // 이미 버프가 걸려 있으면 사용하지 않음
        AttackBuffSystem abs = unit.GetComponent<AttackBuffSystem>();
        if (abs != null && abs.GetTotalBonus() > 0) return null;

        // 공격 액션(밀리 200, 보우 1000+)보다 낮은 우선순위
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 50 };
    }
}
