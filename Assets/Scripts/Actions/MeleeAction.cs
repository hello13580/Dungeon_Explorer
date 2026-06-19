using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeleeAction : BaseAction
{
	private enum State
	{
		Aiming,
		Attack,
		Cooloff
	}

	[SerializeField] private int maxRange = 1;
	[SerializeField] private int damage = 10;
	[SerializeField] private float hitForce = 500f;
	[SerializeField] private float rotateSpeed = 30f;

	private Unit targetUnit;
	private State state;
	private bool canMeleeAttack;

	// 유효 공격 위치 목록 캐시 — 매 UpdateGridVisual마다 점유·팀 체크를 반복하는 비용을 줄임
	private List<GridPosition> cachedValidGridPositionList;
	private bool isCacheDirty = true;

	public event EventHandler OnSwordActionStarted;
    public event EventHandler OnSwordActionEnded;

    protected override void Awake()
	{
		base.Awake();
	}

	private void Start()
	{
		// 턴이 바뀌면 적 위치가 달라질 수 있으므로 캐시 무효화
		TurnSystem.Instance.OnTurnChanged += OnCacheInvalidated;
		// 이동·공격 등 액션이 끝나면 유닛이 죽거나 이동했을 수 있으므로 캐시 무효화
		BaseAction.OnAnyActionEnded += OnCacheInvalidated;
	}

	private void OnDestroy()
	{
		TurnSystem.Instance.OnTurnChanged -= OnCacheInvalidated;
		BaseAction.OnAnyActionEnded -= OnCacheInvalidated;
	}

	private void OnCacheInvalidated(object sender, EventArgs e) => isCacheDirty = true;

	public override string GetActionName() => "Melee";
	public override string GetDescription() =>
		$"인접한 적에게 {damage + unit.GetAttackPower()} 피해를 입힌다.";


	public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
	{
		ActionStart(onActionComplete);
		targetUnit = LevelGrid.Instance.GetUnitListAtGridPosition(gridPosition)[0];
		canMeleeAttack = true;
		state = State.Aiming;
		StartCoroutine(StateMachineRoutine());
	}

	private IEnumerator StateMachineRoutine()
	{
		while (state == State.Aiming)
		{
			AimToTarget();
			yield return null;
		}

		if (canMeleeAttack && state == State.Attack)
		{
			OnSwordActionStarted?.Invoke(this, EventArgs.Empty);
			canMeleeAttack = false;
		}

		yield return new WaitForSeconds(0.3f);
		state = State.Cooloff;

		yield return new WaitForSeconds(0.5f);
        OnSwordActionEnded?.Invoke(this, EventArgs.Empty);
        ActionComplete();
	}

	private void AimToTarget()
	{
		Vector3 targetPos = targetUnit.GetWorldPosition();
		targetPos.y = transform.position.y;

		Vector3 aimDir = (targetPos - transform.position).normalized;
		transform.forward = Vector3.Lerp(transform.forward, aimDir, Time.deltaTime * rotateSpeed);

		if (Vector3.Angle(transform.forward, aimDir) < 1f)
		{
			state = State.Attack;
		}
	}

	public void Melee()
	{
		if (targetUnit == null) return;

		Vector3 targetWorldPos = targetUnit.GetWorldPosition();
		float targetHeight = targetUnit.GetCollider().bounds.max.y;
		targetWorldPos.y = targetHeight * 0.8f;

		Vector3 hitDir = (targetWorldPos - unit.GetWorldPosition()).normalized;

		targetUnit.GetHitReaction().SetHitDirection(hitDir);
		targetUnit.GetHitReaction().SetHitForce(hitForce);
		targetUnit.Damage(unit.CalculateDamage(damage));
	}

	public override List<GridPosition> GetActionRangeGridPositionList()
	{
		List<GridPosition> rangeList = new List<GridPosition>();
		GridPosition unitGridPos = unit.GetGridPosition();
		int unitSize = unit.GetSize();

		for (int x = -maxRange; x < unitSize + maxRange; x++)
		{
			for (int z = -maxRange; z < unitSize + maxRange; z++)
			{
				if (x < 0 || x >= unitSize || z < 0 || z >= unitSize)
				{
					GridPosition testPos = unitGridPos + new GridPosition(x, z, 0);
					if (LevelGrid.Instance.IsValidGridPosition(testPos))
					{
						rangeList.Add(testPos);
					}
				}
			}
		}
		return rangeList;
	}

	public override List<GridPosition> GetValidActionGridPositionList()
	{
		// [최적화 전] 매 호출마다 인접 타일 전체를 순회하며 점유·팀 체크
		// List<GridPosition> validList = new List<GridPosition>();
		// GridPosition unitGridPos = unit.GetGridPosition();
		// int unitSize = unit.GetSize();
		// ... (아래 로직 그대로)

		// [최적화 후] 캐시가 유효하면 재계산 없이 바로 반환
		if (!isCacheDirty && cachedValidGridPositionList != null) return cachedValidGridPositionList;

		List<GridPosition> validList = new List<GridPosition>();
		GridPosition unitGridPos = unit.GetGridPosition();
		int unitSize = unit.GetSize();

		for (int x = -maxRange; x <= (unitSize - 1) + maxRange; x++)
		{
			for (int z = -maxRange; z <= (unitSize - 1) + maxRange; z++)
			{
				if (x >= 0 && x < unitSize && z >= 0 && z < unitSize) continue;

				GridPosition testPos = unitGridPos + new GridPosition(x, z, 0);

				if (!LevelGrid.Instance.IsValidGridPosition(testPos)) continue;
				if (!LevelGrid.Instance.IsGridPositionOccupied(testPos)) continue;

				Unit target = LevelGrid.Instance.GetUnitListAtGridPosition(testPos)[0];

				if (TeamHelper.IsHostile(unit.GetTeamType(), target.GetTeamType()) && !target.IsStealthed())
				{
					validList.Add(testPos);
				}
			}
		}
		cachedValidGridPositionList = validList;
		isCacheDirty = false;
		return cachedValidGridPositionList;
	}

	public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
	{
		return new EnemyAIAction
		{
			gridPosition = gridPosition,
			actionValue = 200
		};
	}
}
