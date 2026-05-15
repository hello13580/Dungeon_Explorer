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
