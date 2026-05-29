using System.Collections.Generic;

public class GridObject
{
	private GridSystem<GridObject> gridSystem;

	private GridPosition gridPosition;

	private List<Unit> unitList;

	private MapObject mapObject;

	public List<Unit> GetUnitList()
	{
		return unitList;
	}

	public bool HasAnyUnit()
	{
		return unitList.Count > 0;
	}

	public void AddUnit(Unit unit)
	{
		unitList.Add(unit);
	}

	public void RemoveUnit(Unit unit)
	{
		unitList.Remove(unit);
	}

	public GridObject(GridSystem<GridObject> gridSystem, GridPosition gridPosition)
	{
		this.gridSystem = gridSystem;
		this.gridPosition = gridPosition;
		unitList = new List<Unit>();
	}

	// 스테이지 전환 시 셀 내용만 초기화 (GridObject 자체는 재사용)
	public void Clear()
	{
		unitList.Clear();
		mapObject = null;
	}

	public override string ToString()
	{
		string text = "";
		foreach (Unit unit in unitList)
		{
			text = text + ((object)unit).ToString() + "\n";
		}
		return gridPosition.ToString() + "\n" + text;
	}

	public MapObject GetMapObject()
	{
		return mapObject;
	}

	public void SetMapObject(MapObject mapObject)
	{
		this.mapObject = mapObject;
	}
}
