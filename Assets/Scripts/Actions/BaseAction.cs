using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseAction : MonoBehaviour
{
	protected Unit unit;

	protected Action onActionComplete;

	[SerializeField]
	protected int actionCost;

	public static event EventHandler OnAnyActionStarted;

	public static event EventHandler OnAnyActionEnded;

	protected virtual void Awake()
	{
		unit = GetComponent<Unit>();
	}

	public abstract string GetActionName();

	public abstract void TakeAction(GridPosition gridPosition, Action onActionComplete);

	public virtual List<GridPosition> GetDamageAffectedGridPosition(GridPosition targetGridPosition)
	{
		if (!LevelGrid.Instance.IsValidGridPosition(targetGridPosition))
		{
			return new List<GridPosition>();
		}
		if (!IsValidActionGridPosition(targetGridPosition))
		{
			return new List<GridPosition>();
		}
		return new List<GridPosition> { targetGridPosition };
	}

	public virtual bool IsValidActionGridPosition(GridPosition gridPosition)
	{
		return GetValidActionGridPositionList().Contains(gridPosition);
	}

	public abstract List<GridPosition> GetValidActionGridPositionList();

	public virtual List<GridPosition> GetActionRangeGridPositionList()
	{
		return GetValidActionGridPositionList();
	}

	public Unit GetUnit()
	{
		return unit;
	}

	public int GetActionPointCost()
	{
		return actionCost;
	}

	protected void ActionStart(Action onActionComplete)
	{
		this.onActionComplete = onActionComplete;
		BaseAction.OnAnyActionStarted?.Invoke(this, EventArgs.Empty);
	}

	protected void ActionComplete()
	{
		onActionComplete();
		BaseAction.OnAnyActionEnded?.Invoke(this, EventArgs.Empty);
	}


	public EnemyAIAction GetBestEnemyAIAction()
	{
		List<EnemyAIAction> list = new List<EnemyAIAction>();
		foreach (GridPosition validActionGridPosition in GetValidActionGridPositionList())
		{
			EnemyAIAction enemyAIAction = GetEnemyAIAction(validActionGridPosition);
			if (enemyAIAction != null)
			{
				list.Add(enemyAIAction);
			}
		}
		if (list.Count > 0)
		{
			list.Sort((EnemyAIAction a, EnemyAIAction b) => b.actionValue - a.actionValue);
			return list[0];
		}
		return null;
	}

	public abstract EnemyAIAction GetEnemyAIAction(GridPosition gridPosition);
}
