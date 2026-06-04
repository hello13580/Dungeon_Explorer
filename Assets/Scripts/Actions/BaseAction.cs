using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseAction : MonoBehaviour
{
	protected Unit unit;

	protected Action onActionComplete;

	[SerializeField]
	protected int actionCost;

	[SerializeField]
	protected int manaCost = 0;

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

	/// <summary>
	/// 두 번째 색상으로 표시할 타일 목록. 기본값은 빈 리스트.
	/// GridSystemVisual에서 GetSecondaryHighlightColor()로 지정한 색상으로 그려진다.
	/// </summary>
	public virtual List<GridPosition> GetSecondaryHighlightGridPositionList()
	{
		return new List<GridPosition>();
	}

	public virtual GridSystemVisual.GridVisualType GetSecondaryHighlightColor()
	{
		return GridSystemVisual.GridVisualType.Yellow;
	}

	public Unit GetUnit()
	{
		return unit;
	}

	public int GetActionPointCost() => actionCost;
	public int GetManaCost() => manaCost;

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
				// 도발 중인 유닛이 있으면 그 위치를 타겟으로 하는 액션에 압도적인 보너스
				if (TauntManager.Instance != null && TauntManager.Instance.HasActiveTaunt())
				{
					Unit taunted = TauntManager.Instance.GetTauntedUnit();
					if (taunted != null && enemyAIAction.gridPosition == taunted.GetGridPosition())
						enemyAIAction.actionValue += 500;
					else
						enemyAIAction.actionValue -= 200;
				}
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
