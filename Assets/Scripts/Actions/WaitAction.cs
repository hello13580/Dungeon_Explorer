using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaitAction : BaseAction
{
    protected override string DefaultActionName() => "대기";
	protected override void Awake()
	{
		base.Awake();
		actionCost = 1;
	}
	public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
	{
		Debug.Log("적 대기 실행");
		ActionStart(onActionComplete);
		StartCoroutine(CompleteNextFrame());
	}

	private IEnumerator CompleteNextFrame()
	{
		yield return null;
		ActionComplete();
	}

	public override List<GridPosition> GetValidActionGridPositionList()
	{
		return new List<GridPosition> { unit.GetGridPosition() };
	}

	public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
	{
		return new EnemyAIAction
		{
			gridPosition = gridPosition,
			actionValue = -30
		};
	}
}
