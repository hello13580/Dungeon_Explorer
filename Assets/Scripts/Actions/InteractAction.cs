using System;
using System.Collections.Generic;
using UnityEngine;

public class InteractAction : BaseAction
{
	[SerializeField]
	private int maxRange;

	[SerializeField]
	private LayerMask obstacleLayerMask;

	// 유효 상호작용 위치 목록 캐시 — 매 UpdateGridVisual마다 MapObject 체크를 반복하는 비용을 줄임
	private List<GridPosition> cachedValidGridPositionList;
	private bool isCacheDirty = true;

	protected override void Awake()
	{
		actionCost = 1;
		base.Awake();
		maxRange = 1;
	}

	private void Start()
	{
		// 턴이 바뀌면 오브젝트 상태가 달라질 수 있으므로 캐시 무효화
		TurnSystem.Instance.OnTurnChanged += OnTurnChanged;
		// 문 열기 등 액션이 끝나면 상호작용 가능 오브젝트가 바뀔 수 있으므로 캐시 무효화
		BaseAction.OnAnyActionEnded += OnAnyActionEnded;
	}

	private void OnDestroy()
	{
		TurnSystem.Instance.OnTurnChanged -= OnTurnChanged;
		BaseAction.OnAnyActionEnded -= OnAnyActionEnded;
	}

	private void OnTurnChanged(object sender, EventArgs e) => isCacheDirty = true;
	private void OnAnyActionEnded(object sender, EventArgs e) => isCacheDirty = true;

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
		// [최적화 전] 매 호출마다 인접 타일 전체를 순회하며 MapObject 체크
		// return GetValidActionGridPositionList(unit.GetGridPosition());

		// [최적화 후] 캐시가 유효하면 재계산 없이 바로 반환
		if (!isCacheDirty && cachedValidGridPositionList != null) return cachedValidGridPositionList;

		cachedValidGridPositionList = GetValidActionGridPositionList(unit.GetGridPosition());
		isCacheDirty = false;
		return cachedValidGridPositionList;
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
