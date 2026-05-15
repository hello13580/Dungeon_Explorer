using System;
using System.Collections.Generic;
using UnityEngine;

public class PathFinding : MonoBehaviour
{
	private const int MOVE_STRAIGHT_COST = 10;

	private const int MOVE_DIAGONAL_COST = 14;

	[SerializeField]
	private GameObject gridDebugObjectPrefab;

	[SerializeField]
	private LayerMask obstacleLayerMask;

	[SerializeField]
	private LayerMask floorLayerMask;

	[SerializeField]
	private Transform PathfindingLinkContainer;

	private int width;

	private int height;

	private float cellSize;

	private int floorAmount;

	private List<GridSystem<PathNode>> gridSystemList;

	private List<PathFindingLink> pathFindingLinkList;

	private int currentSearchId;

	public static PathFinding Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Debug.LogError("There's more than one PathFinding! " + transform + " - " + Instance);
			Destroy(gameObject);
		}
		else
		{
			Instance = this;
			DontDestroyOnLoad(gameObject);
		}
	}

	private void Start()
	{
	}

	private void Unit_OnAnyUnitSpawned(object sender, EventArgs e)
	{
		if (sender is Unit unit && unit.gameObject.layer == LayerMask.NameToLayer("Object"))
		{
			GridPosition gridPosition = unit.GetGridPosition();
			SetIsWalkable(gridPosition, isWalkable: false);
		}
	}

	public void Setup(int width, int height, float cellsize, int floorAmount)
	{
		this.width = width;
		this.height = height;
		cellSize = cellsize;
		this.floorAmount = floorAmount;
		gridSystemList = new List<GridSystem<PathNode>>();
		for (int i = 0; i < floorAmount; i++)
		{
			GridSystem<PathNode> item = new GridSystem<PathNode>(width, height, cellsize, i, 3f, (GridSystem<PathNode> g, GridPosition gridPosition) => new PathNode(gridPosition));
			gridSystemList.Add(item);
			for (int j = 0; j < width; j++)
			{
				for (int k = 0; k < height; k++)
				{
					GridPosition gridPosition2 = new GridPosition(j, k, i);
					Vector3 worldPosition = LevelGrid.Instance.GetWorldPosition(gridPosition2);
					float num = 0.8f;
					if (Physics.CheckSphere(worldPosition, num, obstacleLayerMask))
					{
						GetNode(j, k, i).SetIsWalkable(isWalkable: false);
						continue;
					}
					float num2 = 1.5f;
					if (!Physics.Raycast(worldPosition + Vector3.up * 0.5f, Vector3.down, num2, floorLayerMask))
					{
						GetNode(j, k, i).SetIsWalkable(isWalkable: false);
					}
				}
			}
		}
		pathFindingLinkList = new List<PathFindingLink>();
		foreach (Transform child in PathfindingLinkContainer)
		{
			if (child.TryGetComponent<PathfindingLinkMonoBehaviour>(out var link))
			{
				pathFindingLinkList.Add(link.GetPathfindingLink());
			}
		}
	}

	public List<GridPosition> FindPath(GridPosition startGridPosition, GridPosition endGridPosition, int unitSize, out int pathLength)
	{
		currentSearchId++;
		Heap<PathNode> heap = new Heap<PathNode>(width * height);
		HashSet<PathNode> hashSet = new HashSet<PathNode>();
		PathNode gridObject = GetGridSystem(startGridPosition.floor).GetGridObject(startGridPosition);
		PathNode gridObject2 = GetGridSystem(endGridPosition.floor).GetGridObject(endGridPosition);
		gridObject.Reset(currentSearchId);
		gridObject.SetGCost(0);
		gridObject.SetHCost(CalculateDistanceCost(startGridPosition, endGridPosition));
		gridObject.CalculateFCost();
		heap.Add(gridObject);
		while (heap.Count > 0)
		{
			PathNode pathNode = heap.RemoveFirst();
			hashSet.Add(pathNode);
			if (pathNode == gridObject2)
			{
				pathLength = gridObject2.GetFCost();
				return CalculatePath(gridObject2);
			}
			foreach (PathNode neighbor in GetNeighborList(pathNode, unitSize))
			{
				if (neighbor.LastSearchId != currentSearchId)
				{
					neighbor.Reset(currentSearchId);
				}
				if (hashSet.Contains(neighbor) || !IsWalkableArea(neighbor.GetGridPosition(), unitSize, neighbor.GetGridPosition().floor))
				{
					continue;
				}
				int num = pathNode.GetGCost() + CalculateDistanceCost(pathNode.GetGridPosition(), neighbor.GetGridPosition());
				if (num < neighbor.GetGCost())
				{
					neighbor.SetCameFromPathNode(pathNode);
					neighbor.SetGCost(num);
					neighbor.SetHCost(CalculateDistanceCost(neighbor.GetGridPosition(), endGridPosition));
					neighbor.CalculateFCost();
					if (!heap.Contains(neighbor))
					{
						heap.Add(neighbor);
					}
					else
					{
						heap.UpdateItem(neighbor);
					}
				}
			}
		}
		pathLength = 0;
		return null;
	}

	private List<GridPosition> CalculatePath(PathNode endNode)
	{
		List<GridPosition> list = new List<GridPosition>();
		PathNode pathNode = endNode;
		while (pathNode != null && pathNode.GetPathNode() != null)
		{
			list.Add(pathNode.GetGridPosition());
			pathNode = pathNode.GetPathNode();
		}
		list.Reverse();
		return list;
	}

	public int CalculateDistanceCost(GridPosition gridPositionA, GridPosition gridPositionB)
	{
		int num = Mathf.Abs(gridPositionA.x - gridPositionB.x);
		int num2 = Mathf.Abs(gridPositionA.z - gridPositionB.z);
		int num3 = Mathf.Abs(num - num2);
		return 14 * Mathf.Min(num, num2) + 10 * num3;
	}

	private GridSystem<PathNode> GetGridSystem(int floor)
	{
		return gridSystemList[floor];
	}

	public PathNode GetNode(int x, int z, int floor)
	{
		return GetGridSystem(floor).GetGridObject(new GridPosition(x, z, floor));
	}

	public void SetIsWalkable(GridPosition gridPosition, bool isWalkable)
	{
		GetNode(gridPosition.x, gridPosition.z, gridPosition.floor)?.SetIsWalkable(isWalkable);
	}

	private List<PathNode> GetNeighborList(PathNode currentNode, int currentUnitSize)
	{
		List<PathNode> list = new List<PathNode>();
		GridPosition gridPosition = currentNode.GetGridPosition();
		for (int i = -1; i <= 1; i++)
		{
			for (int j = -1; j <= 1; j++)
			{
				if (i == 0 && j == 0)
				{
					continue;
				}
				int num = gridPosition.x + i;
				int num2 = gridPosition.z + j;
				if (num < 0 || num >= width || num2 < 0 || num2 >= height)
				{
					continue;
				}
				for (int k = 0; k < floorAmount; k++)
				{
					GridPosition gridPosition2 = new GridPosition(num, num2, k);
					if (IsWalkableArea(gridPosition2, currentUnitSize, k))
					{
						PathNode node = GetNode(gridPosition2.x, gridPosition2.z, k);
						if (Vector3.Distance(LevelGrid.Instance.GetWorldPosition(currentNode.GetGridPosition()), LevelGrid.Instance.GetWorldPosition(node.GetGridPosition())) < cellSize * 1.5f && (Mathf.Abs(i) != 1 || Mathf.Abs(j) != 1 || (IsWalkableArea(new GridPosition(gridPosition.x + i, gridPosition.z, k), currentUnitSize, k) && IsWalkableArea(new GridPosition(gridPosition.x, gridPosition.z + j, k), currentUnitSize, k))))
						{
							list.Add(node);
							break;
						}
					}
				}
			}
		}
		foreach (GridPosition pathfindingLinkConnectedGridPosition in GetPathfindingLinkConnectedGridPositionList(gridPosition))
		{
			if (IsWalkableArea(pathfindingLinkConnectedGridPosition, currentUnitSize, pathfindingLinkConnectedGridPosition.floor))
			{
				list.Add(GetNode(pathfindingLinkConnectedGridPosition.x, pathfindingLinkConnectedGridPosition.z, pathfindingLinkConnectedGridPosition.floor));
			}
		}
		return list;
	}

	private List<GridPosition> GetPathfindingLinkConnectedGridPositionList(GridPosition gridPosition)
	{
		List<GridPosition> list = new List<GridPosition>();
		foreach (PathFindingLink pathFindingLink in pathFindingLinkList)
		{
			if (pathFindingLink.gridPositionA == gridPosition)
			{
				list.Add(pathFindingLink.gridPositionB);
			}
			if (pathFindingLink.gridPositionB == gridPosition)
			{
				list.Add(pathFindingLink.gridPositionA);
			}
		}
		return list;
	}

	public bool IsWalkableGridPosition(GridPosition gridPosition)
	{
		return GetNode(gridPosition.x, gridPosition.z, gridPosition.floor).IsWalkable();
	}

	public int GetPathLength(GridPosition startGridPosition, GridPosition endGridPosition, int unitSize)
	{
		FindPath(startGridPosition, endGridPosition, unitSize, out var pathLength);
		return pathLength;
	}

	public bool IsWalkableArea(GridPosition gridPosition, int size, int floor)
	{
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				GridPosition gridPosition2 = new GridPosition(gridPosition.x + i, gridPosition.z + j, floor);
				if (gridPosition2.x < 0 || gridPosition2.x >= width || gridPosition2.z < 0 || gridPosition2.z >= height)
				{
					return false;
				}
				if (!GetNode(gridPosition2.x, gridPosition2.z, floor).IsWalkable())
				{
					return false;
				}
			}
		}
		return true;
	}
}
