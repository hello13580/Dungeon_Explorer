using System;
using UnityEngine;

public class GridSystem<TGridObject>
{
	private int width;

	private int height;

	private float cellSize;

	private int floor;

	[SerializeField]
	private float floorHeight;

	private TGridObject[,] gridObjectsArray;

	public GridSystem(int width, int height, float cellSize, int floor, float floorHeight, Func<GridSystem<TGridObject>, GridPosition, TGridObject> createGridObject)
	{
		this.width = width;
		this.height = height;
		this.cellSize = cellSize;
		this.floor = floor;
		this.floorHeight = floorHeight;
		gridObjectsArray = new TGridObject[width, height];
		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				GridPosition arg = new GridPosition(i, j, floor);
				gridObjectsArray[i, j] = createGridObject(this, arg);
			}
		}
	}

	public TGridObject[,] GetAllGridObjects()
	{
		return gridObjectsArray;
	}

	public Vector3 GetWorldPosition(GridPosition gridPosition)
	{
		return new Vector3((float)gridPosition.x, 0f, (float)gridPosition.z) * cellSize + new Vector3(0f, (float)floor, 0f) * floorHeight;
	}

	public GridPosition GetGridPosition(Vector3 worldPosition)
	{
		return new GridPosition(Mathf.RoundToInt(worldPosition.x / cellSize), Mathf.RoundToInt(worldPosition.z / cellSize), floor);
	}

	public void CreateDebugObject(GameObject debugPrefab)
	{
		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				GridPosition gridPosition = new GridPosition(i, j, floor);
				UnityEngine.Object.Instantiate<GameObject>(debugPrefab, new Vector3(GetWorldPosition(gridPosition).x, 0.02f, GetWorldPosition(gridPosition).z), Quaternion.identity).GetComponent<GridDebugObject>().SetGridObject(GetGridObject(gridPosition));
			}
		}
	}

	public TGridObject GetGridObject(GridPosition gridPosition)
	{
		if (!IsValidGridPosition(gridPosition))
		{
			return default(TGridObject);
		}
		return gridObjectsArray[gridPosition.x, gridPosition.z];
	}

	public bool IsValidGridPosition(GridPosition gridPosition)
	{
		if (gridPosition.x >= 0 && gridPosition.z >= 0 && gridPosition.x < width && gridPosition.z < height)
		{
			return gridPosition.floor == floor;
		}
		return false;
	}

	public int GetWidth()
	{
		return width;
	}

	public int GetHeight()
	{
		return height;
	}
}
