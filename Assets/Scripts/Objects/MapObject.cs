using System;
using UnityEngine;

public abstract class MapObject : MonoBehaviour, IInteractable
{
	protected GridPosition gridPosition;

	[SerializeField]
	protected bool isWalkable;

	protected virtual void Awake()
	{
	}

	protected virtual void Start()
	{
		gridPosition = LevelGrid.Instance.GetGridPosition(transform.position);
		LevelGrid.Instance.AddMapObjectToGridPosition(gridPosition, this);
		PathFinding.Instance.SetIsWalkable(gridPosition, isWalkable);
	}

	public abstract string GetObjectName();

	public bool IsWalkable()
	{
		return isWalkable;
	}

	public virtual void Interact(Action onInteractComplete)
	{
		onInteractComplete();
	}

	public void SetIsWalkable(bool isWalkable)
	{
		this.isWalkable = isWalkable;
		PathFinding.Instance.SetIsWalkable(gridPosition, isWalkable);
	}
}
