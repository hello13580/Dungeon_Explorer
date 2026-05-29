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
		width = 100;
		height = 100;
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

	private void OnDrawGizmos()
	{
		// 에디터에서는 Inspector 값, 플레이 중에는 Awake에서 덮어쓴 값 사용
		int w = (width > 0) ? width : 100;
		int h = (height > 0) ? height : 100;
		float cs = (cellSize > 0f) ? cellSize : 2f;
		int floors = (floorAmount > 0) ? floorAmount : 1;

		for (int i = 0; i < floors; i++)
		{
			float floorY = i * FLOOR_HEIGHT;

			// 그리드 전체 외곽선 (노란색)
			Gizmos.color = Color.yellow;
			Vector3 center = new Vector3((w - 1) * cs / 2f, floorY, (h - 1) * cs / 2f);
			Vector3 size = new Vector3(w * cs, 0.05f, h * cs);
			Gizmos.DrawWireCube(center, size);

			// 네 코너 마커 (빨간색)
			Gizmos.color = Color.red;
			float markerSize = cs * 0.4f;
			Gizmos.DrawWireSphere(new Vector3(0f,              floorY, 0f),              markerSize);
			Gizmos.DrawWireSphere(new Vector3((w - 1) * cs,   floorY, 0f),              markerSize);
			Gizmos.DrawWireSphere(new Vector3(0f,              floorY, (h - 1) * cs),   markerSize);
			Gizmos.DrawWireSphere(new Vector3((w - 1) * cs,   floorY, (h - 1) * cs),   markerSize);
		}
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
		return Mathf.Clamp(Mathf.RoundToInt(worldPosition.y / FLOOR_HEIGHT), 0, gridSystemList.Count - 1);
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

	/// <summary>
	/// 스테이지 전환 시 호출. 그리드 데이터를 새로 생성한다.
	/// PathFinding.Setup()은 맵 프리팹 인스턴시에이트 이후 StageManager가 별도로 호출한다.
	/// </summary>
	public void ResetGridSystems(int newFloorAmount)
	{
		floorAmount = newFloorAmount;
		gridSystemList = new List<GridSystem<GridObject>>();
		for (int i = 0; i < floorAmount; i++)
		{
			GridSystem<GridObject> item = new GridSystem<GridObject>(
				width, height, cellSize, i, FLOOR_HEIGHT,
				(GridSystem<GridObject> g, GridPosition p) => new GridObject(g, p));
			gridSystemList.Add(item);
		}
	}
}
