using System.Collections.Generic;
using UnityEngine;

public class LevelGrid : MonoBehaviour
{
	[SerializeField]
	private GameObject gridDebugObjectPrefab;

	public const float FLOOR_HEIGHT = 3f;

	private List<GridSystem<GridObject>> gridSystemList;

	[SerializeField]
	private LayerMask floorLayerMask;

	[SerializeField]
	private int width;

	[SerializeField]
	private int height;

	[SerializeField]
	private float cellSize;

	[SerializeField]
	private int floorAmount;

	public static LevelGrid Instance { get; private set; }

	private void Awake()
	{
		width = 50;
		height = 50;
		cellSize = 2f;
		if (Instance != null && Instance != this)
		{
			Debug.LogError("There's more than one LevelGrid! " + transform + " - " + Instance);
			Destroy(gameObject);
			return;
		}
		Instance = this;
		DontDestroyOnLoad(gameObject);
		gridSystemList = new List<GridSystem<GridObject>>();
		for (int i = 0; i < floorAmount; i++)
		{
			GridSystem<GridObject> item = new GridSystem<GridObject>(width, height, cellSize, i, 3f, (GridSystem<GridObject> g, GridPosition p) => new GridObject(g, p));
			gridSystemList.Add(item);
		}
		// MapObject.Start()가 LevelGrid.Start()보다 먼저 실행될 수 있어서
		// Start()에 두면 PathFinding.gridSystemList가 null인 상태에서 SetIsWalkable이 호출됨
		// Awake는 모든 오브젝트에서 Start보다 먼저 완료되므로 여기서 초기화해야 함
		PathFinding.Instance.Setup(width, height, cellSize, floorAmount);
	}

	private void Start()
	{
	}

	private void Update()
	{
	}

	private GridSystem<GridObject> GetGridSystem(int floor)
	{
		return gridSystemList[floor];
	}

	public void AddUnitAtGridPosition(GridPosition gridPosition, Unit unit)
	{
		int size = unit.GetSize();
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				GridPosition gridPosition2 = gridPosition + new GridPosition(i, j, 0);
				if (IsValidGridPosition(gridPosition2))
				{
					GetGridSystem(gridPosition.floor).GetGridObject(gridPosition2).AddUnit(unit);
				}
			}
		}
	}

	public List<Unit> GetUnitListAtGridPosition(GridPosition gridPosition)
	{
		if (IsGridPositionOccupied(gridPosition))
		{
			return GetGridSystem(gridPosition.floor).GetGridObject(gridPosition).GetUnitList();
		}
		return new List<Unit>();
	}

	public void RemoveUnitAtGridPosition(GridPosition gridPosition, Unit unit)
	{
		int size = unit.GetSize();
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				GridPosition gridPosition2 = gridPosition + new GridPosition(i, j, 0);
				if (IsValidGridPosition(gridPosition2))
				{
					GetGridSystem(gridPosition.floor).GetGridObject(gridPosition2).RemoveUnit(unit);
				}
			}
		}
	}

	public void UnitMovedGridPosition(Unit unit, GridPosition fromGridPosition, GridPosition toGridPosition)
	{
		RemoveUnitAtGridPosition(fromGridPosition, unit);
		AddUnitAtGridPosition(toGridPosition, unit);
	}

	public int GetFloor(Vector3 worldPosition)
	{
		return Mathf.Clamp(Mathf.FloorToInt(worldPosition.y + 1f / 60f), 0, gridSystemList.Count - 1);
	}

	public GridPosition GetGridPosition(Vector3 worldPosition)
	{
		return GetGridSystem(GetFloor(worldPosition)).GetGridPosition(worldPosition);
	}

	public Vector3 GetWorldPosition(GridPosition gridPosition)
	{
		return GetGridSystem(gridPosition.floor).GetWorldPosition(gridPosition);
	}

	public bool IsValidGridPosition(GridPosition gridPosition)
	{
		if (gridPosition.floor < 0 || gridPosition.floor >= floorAmount)
		{
			return false;
		}
		return GetGridSystem(gridPosition.floor).IsValidGridPosition(gridPosition);
	}

	public int GetWidth()
	{
		return GetGridSystem(0).GetWidth();
	}

	public int GetHeight()
	{
		return GetGridSystem(0).GetHeight();
	}

	public int GetFloorAmount()
	{
		return floorAmount;
	}

	public bool IsGridPositionOccupied(GridPosition gridPosition)
	{
		return GetGridSystem(gridPosition.floor).GetGridObject(gridPosition).HasAnyUnit();
	}

	public MapObject GetMapObjectAtGridPosition(GridPosition gridPosition)
	{
		return GetGridSystem(gridPosition.floor).GetGridObject(gridPosition).GetMapObject();
	}

	public void AddMapObjectToGridPosition(GridPosition gridPosition, MapObject mapObject)
	{
		GetGridSystem(gridPosition.floor).GetGridObject(gridPosition).SetMapObject(mapObject);
	}

	public void RemoveMapObjectToGridPosition(GridPosition gridPosition)
	{
		GetGridSystem(gridPosition.floor).GetGridObject(gridPosition).SetMapObject(null);
	}

	public float GetCellSize()
	{
		return cellSize;
	}
}
