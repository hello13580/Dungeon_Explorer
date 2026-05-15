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
	[SerializeField] private int damage = 100;
	[SerializeField] private float hitForce = 500f;
	[SerializeField] private float rotateSpeed = 30f;

	private Unit targetUnit;
	private State state;
	private bool canMeleeAttack;

	public event EventHandler OnSwordActionStarted;

	protected override void Awake()
	{
		base.Awake();
		actionCost = 1;
	}

	public override string GetActionName() => "Melee";

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

		yield return new WaitForSeconds(0.2f);
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
		targetUnit.Damage(damage);
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
		return validList;
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
