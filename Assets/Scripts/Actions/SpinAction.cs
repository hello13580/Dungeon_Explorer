using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpinAction : BaseAction
{
	[SerializeField]
	private float totalSpinAmount;

	[SerializeField]
	private float spinSpeed;

	private float currentSpinAmount;

	protected override void Awake()
	{
		base.Awake();
		actionCost = 1;
	}

	private void Start()
	{
		totalSpinAmount = 180f;
		spinSpeed = 360f;
		currentSpinAmount = totalSpinAmount;
	}

	public override void TakeAction(GridPosition gridPosition, Action onSpinComplete)
	{
		currentSpinAmount = 0f;
		ActionStart(onSpinComplete);
		StartCoroutine(Spin());
	}

	private IEnumerator Spin()
	{
		while (currentSpinAmount <= totalSpinAmount)
		{
			float num = spinSpeed * Time.deltaTime;
			currentSpinAmount += num;
			transform.eulerAngles += new Vector3(0f, num, 0f);
			yield return null;
		}
		currentSpinAmount = 0f;
		ActionComplete();
	}

	public override List<GridPosition> GetValidActionGridPositionList()
	{
		new List<GridPosition>();
		GridPosition gridPosition = unit.GetGridPosition();
		return new List<GridPosition> { gridPosition };
	}

	public override string GetActionName()
	{
		return "Spin";
	}

	public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
	{
		return new EnemyAIAction
		{
			gridPosition = gridPosition,
			actionValue = -50
		};
	}
}
