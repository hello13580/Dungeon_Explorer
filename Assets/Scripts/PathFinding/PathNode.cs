using System;

public class PathNode : IHeapItem<PathNode>, IComparable<PathNode>
{
	private GridPosition gridPosition;

	private int gCost;

	private int hCost;

	private int fCost;

	private PathNode cameFromPahtNode;

	private bool isWalkable = true;

	private int heapIndex;

	private int lastSearchId;

	public int LastSearchId
	{
		get
		{
			return lastSearchId;
		}
		set
		{
			lastSearchId = value;
		}
	}

	public int HeapIndex
	{
		get
		{
			return heapIndex;
		}
		set
		{
			heapIndex = value;
		}
	}

	public void Reset(int searchId)
	{
		lastSearchId = searchId;
		gCost = int.MaxValue;
		hCost = 0;
		CalculateFCost();
		cameFromPahtNode = null;
	}

	public PathNode(GridPosition gridPosition)
	{
		this.gridPosition = gridPosition;
	}

	public int CompareTo(PathNode other)
	{
		int num = fCost.CompareTo(other.fCost);
		if (num == 0)
		{
			num = hCost.CompareTo(other.hCost);
		}
		return -num;
	}

	public override string ToString()
	{
		return gridPosition.ToString();
	}

	public int GetGCost()
	{
		return gCost;
	}

	public int GetHCost()
	{
		return hCost;
	}

	public int GetFCost()
	{
		return fCost;
	}

	public void SetGCost(int gCost)
	{
		this.gCost = gCost;
	}

	public void SetHCost(int hCost)
	{
		this.hCost = hCost;
	}

	public void CalculateFCost()
	{
		fCost = gCost + hCost;
	}

	public void ResetcameFromPathNode()
	{
		cameFromPahtNode = null;
	}

	public GridPosition GetGridPosition()
	{
		return gridPosition;
	}

	public void SetCameFromPathNode(PathNode pathNode)
	{
		cameFromPahtNode = pathNode;
	}

	public PathNode GetPathNode()
	{
		return cameFromPahtNode;
	}

	public bool IsWalkable()
	{
		return isWalkable;
	}

	public void SetIsWalkable(bool isWalkable)
	{
		this.isWalkable = isWalkable;
	}
}
