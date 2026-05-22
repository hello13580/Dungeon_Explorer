using System;
using UnityEngine;

public abstract class MapObject : MonoBehaviour, IInteractable
{
	// 문 같은 오브젝트의 이동 가능 여부가 바뀔 때 발생. MoveAction의 캐시 무효화에 사용됨
	public static event EventHandler OnAnyWalkableChanged;

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
		// PathFinding 그리드 갱신 후 MoveAction 캐시도 무효화되도록 이벤트 발생
		OnAnyWalkableChanged?.Invoke(this, EventArgs.Empty);
	}
}
