using System;
using UnityEngine;

public class Unit : MonoBehaviour
{
	private const int ACTION_POINTS_INITIAL = 0;

	private GridPosition savedPosition;

	private HealthSystem healthSystem;

	private HitReactionSystem hitReactionSystem;

	private BaseAction[] baseActionArray;

	[SerializeField]
	private int currentActionPoint;

	[SerializeField]
	private int maxActionPoint;

	[SerializeField]
	private TeamType teamType;

	[SerializeField]
	private float initialSpeed;

	[SerializeField]
	private float currentSpeed;

	[SerializeField]
	private float actionGauge;

	[SerializeField]
	private Transform ShootPointTransform;

	[SerializeField]
	private int size = 1;

	private bool isStealthed;

	public static event EventHandler OnAnyActionPointsChanged;

	public static event EventHandler OnAnyUnitSpawned;

	public static event EventHandler OnAnyUnitDead;

	private void Awake()
	{
		healthSystem = GetComponent<HealthSystem>();
		hitReactionSystem = GetComponent<HitReactionSystem>();
		baseActionArray = GetComponents<BaseAction>();
		currentActionPoint = maxActionPoint;
		currentSpeed = initialSpeed;
		actionGauge = 0f;
	}

	private void Start()
	{
		savedPosition = LevelGrid.Instance.GetGridPosition(transform.position);
		float cellSize = LevelGrid.Instance.GetCellSize();
		float num = (float)(size - 1) * cellSize * 0.5f;
		Vector3 worldPosition = LevelGrid.Instance.GetWorldPosition(savedPosition);
		transform.position = new Vector3(worldPosition.x + num, transform.position.y, worldPosition.z + num);
		LevelGrid.Instance.AddUnitAtGridPosition(savedPosition, this);
		TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
		//healthSystem.OnUnitDeath += HealthSystem_OnUnitDeath;
		Unit.OnAnyUnitSpawned?.Invoke(this, EventArgs.Empty);
	}

	public T GetAction<T>() where T : BaseAction
	{
		BaseAction[] array = baseActionArray;
		foreach (BaseAction baseAction in array)
		{
			if (baseAction is T)
			{
				return baseAction as T;
			}
		}
		return null;
	}

	public bool IsStealthed()
	{
		return isStealthed;
	}

	public void SetStealth(bool state)
	{
		isStealthed = state;
	}

	public GridPosition GetGridPosition()
	{
		return savedPosition;
	}

	public Vector3 GetWorldPosition()
	{
		Vector3 worldPosition = LevelGrid.Instance.GetWorldPosition(GetGridPosition());
		float cellSize = LevelGrid.Instance.GetCellSize();
		float num = (float)(size - 1) * cellSize * 0.5f;
		return worldPosition + new Vector3(num, 0f, num);
	}

	public BaseAction[] GetBaseActionsArray()
	{
		return baseActionArray;
	}

	public bool CanSpendActionPointsToTakeAction(BaseAction baseAction)
	{
		if (currentActionPoint >= baseAction.GetActionPointCost())
		{
			return true;
		}
		return false;
	}

	public bool SpendActionPoint(BaseAction baseAction)
	{
		if (CanSpendActionPointsToTakeAction(baseAction))
		{
			currentActionPoint -= baseAction.GetActionPointCost();
			Unit.OnAnyActionPointsChanged?.Invoke(this, EventArgs.Empty);
			return true;
		}
		return false;
	}

	public int GetCurrentActionPoint()
	{
		return currentActionPoint;
	}

	private void TurnSystem_OnTurnChanged(object sender, EventArgs empty)
	{
		if (TurnSystem.Instance.GetTurnUnit() == this)
		{
			currentActionPoint = maxActionPoint;
			Unit.OnAnyActionPointsChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	public TeamType GetTeamType()
	{
		return teamType;
	}

	public void Damage(int damageAmount)
	{
		healthSystem.Damage(damageAmount);
	}

	public HitReactionSystem GetHitReaction()
	{
		return hitReactionSystem;
	}

	public Collider GetCollider()
	{
		return GetComponent<Collider>();
	}

	public void HealthSystem_OnUnitDeath(object sender, EventArgs empty)
	{
		if (gameObject.layer == LayerMask.NameToLayer("Object"))
		{
			PathFinding.Instance.SetIsWalkable(savedPosition, isWalkable: true);
		}
		LevelGrid.Instance.RemoveUnitAtGridPosition(GetGridPosition(), this);
		Unit.OnAnyUnitDead?.Invoke(this, EventArgs.Empty);
		Destroy(gameObject);
	}

	public float GetCurrentSpeed()
	{
		return currentSpeed;
	}

	public float GetCurrentGauge()
	{
		return actionGauge;
	}

	public void AddGauge()
	{
		actionGauge += GetCurrentSpeed();
	}

	public void SubGauge(float subAmount)
	{
		actionGauge -= subAmount;
	}

	public bool CanAct(float gaugeRequired)
	{
		return actionGauge >= gaugeRequired;
	}

	public void ResetGauge()
	{
		actionGauge = 0f;
	}

	public float GetHealthNormalized()
	{
		return healthSystem.GetHealthNormalized();
	}

	public float GetCurrentHealth()
	{
		return healthSystem.GetCurrentHealth();
	}

	public Transform GetShootPointTransform()
	{
		return ShootPointTransform;
	}

	public int GetSize()
	{
		return size;
	}

	public void SetGridPosition(GridPosition newGridPosition)
	{
		if (savedPosition != newGridPosition)
		{
			LevelGrid.Instance.UnitMovedGridPosition(this, savedPosition, newGridPosition);
			savedPosition = newGridPosition;
		}
	}
}
