using System;

public struct GridPosition : IEquatable<GridPosition>
{
	public int x;

	public int z;

	public int floor;

	public GridPosition(int x, int z, int floor)
	{
		this.x = x;
		this.z = z;
		this.floor = floor;
	}

	public override bool Equals(object obj)
	{
		if (obj is GridPosition gridPosition && x == gridPosition.x && z == gridPosition.z)
		{
			return floor == gridPosition.floor;
		}
		return false;
	}

	public bool Equals(GridPosition other)
	{
		return this == other;
	}

	public override int GetHashCode()
	{
		return HashCode.Combine(x, z, floor);
	}

	public override string ToString()
	{
		return $"x: {x} , z: {z}, floor: {floor}";
	}

	public static bool operator ==(GridPosition a, GridPosition b)
	{
		if (a.x == b.x && a.z == b.z)
		{
			return a.floor == b.floor;
		}
		return false;
	}

	public static bool operator !=(GridPosition a, GridPosition b)
	{
		return !(a == b);
	}

	public static GridPosition operator +(GridPosition a, GridPosition b)
	{
		GridPosition result = default(GridPosition);
		result.x = a.x + b.x;
		result.z = a.z + b.z;
		result.floor = a.floor + b.floor;
		return result;
	}

	public static GridPosition operator -(GridPosition a, GridPosition b)
	{
		GridPosition result = default(GridPosition);
		result.x = a.x - b.x;
		result.z = a.z - b.z;
		result.floor = a.floor - b.floor;
		return result;
	}
}
