using System;
using System.Collections.Generic;
using UnityEngine;

public class InteractAction : BaseAction
{
	[SerializeField]
	private int maxRange;

	[SerializeField]
	private LayerMask obstacleLayerMask;

	[SerializeField]
	private Transform shootPointTransform;

	protected override void Awake()
	{
		actionCost = 1;
		base.Awake();
		maxRange = 1;
	}

	public override string GetActionName()
	{
		return "Interact";
	}

	public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
	{
		return new EnemyAIAction
		{
			gridPosition = gridPosition,
			actionValue = -1000
		};
	}

	public override List<GridPosition> GetActionRangeGridPositionList()
	{
		List<GridPosition> list = new List<GridPosition>();
		GridPosition gridPosition = unit.GetGridPosition();
		int size = unit.GetSize();
		for (int i = -maxRange; i < size + maxRange; i++)
		{
			for (int j = -maxRange; j < size + maxRange; j++)
			{
				if (i < 0 || i >= size || j < 0 || j >= size)
				{
					GridPosition gridPosition2 = gridPosition + new GridPosition(i, j, 0);
					if (LevelGrid.Instance.IsValidGridPosition(gridPosition2))
					{
						list.Add(gridPosition2);
					}
				}
			}
		}
		return list;
	}

	public override List<GridPosition> GetValidActionGridPositionList()
	{
		GridPosition gridPosition = unit.GetGridPosition();
		return GetValidActionGridPositionList(gridPosition);
	}

	public List<GridPosition> GetValidActionGridPositionList(GridPosition unitGridPosition)
	{
		List<GridPosition> list = new List<GridPosition>();
		for (int i = -maxRange; i <= maxRange; i++)
		{
			for (int j = -maxRange; j <= maxRange; j++)
			{
				GridPosition gridPosition = new GridPosition(i, j, 0);
				GridPosition gridPosition2 = unitGridPosition + gridPosition;
				if (LevelGrid.Instance.IsValidGridPosition(gridPosition2) && LevelGrid.Instance.GetMapObjectAtGridPosition(gridPosition2) != null)
				{
					list.Add(gridPosition2);
				}
			}
		}
		return list;
	}

	public void Interact(IInteractable mapObject, Action onInteractComplete)
	{
		mapObject.Interact(onInteractComplete);
	}

	public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
	{
		ActionStart(onActionComplete);
		MapObject mapObjectAtGridPosition = LevelGrid.Instance.GetMapObjectAtGridPosition(gridPosition);
		if (mapObjectAtGridPosition != null)
		{
			Interact(mapObjectAtGridPosition, base.ActionComplete);
		}
	}
}
