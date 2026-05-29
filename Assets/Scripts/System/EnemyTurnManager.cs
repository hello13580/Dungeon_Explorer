using System;
using System.Collections;
using UnityEngine;

public class EnemyTurnManager : MonoBehaviour
{
	private enum State
	{
		Idle,
		EnemyTurn,
		Busy
	}

	private State state;

	private Unit currentUnit;

	private void Awake()
	{
		state = State.Idle;
	}

	private void Start()
	{
		TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
	}

	private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
	{
		if (!TurnSystem.Instance.IsPlayerTurn())
		{
			currentUnit = TurnSystem.Instance.GetTurnUnit();

			// currentUnit이 null이면 유효한 적 턴이 아님 (초기화 중 잘못된 이벤트 방지)
			if (currentUnit == null) return;

			state = State.EnemyTurn;
			StartCoroutine(OnEnemyTurnRoutine());
		}
	}

	private IEnumerator OnEnemyTurnRoutine()
	{
		state = State.Busy;
		yield return new WaitForSeconds(0.5f);
		bool isActionTaking = true;
		while (isActionTaking)
		{
			BaseAction bestBaseAction = null;
			EnemyAIAction bestEnemyAIAction = null;
			yield return StartCoroutine(CalculateBestAIActionRoutine(currentUnit, delegate(BaseAction action, EnemyAIAction aiAction)
			{
				bestBaseAction = action;
				bestEnemyAIAction = aiAction;
			}));
			if (bestBaseAction != null && bestEnemyAIAction != null && currentUnit.SpendActionPoint(bestBaseAction))
			{
				bool isWaitingForAction = true;
				bestBaseAction.TakeAction(bestEnemyAIAction.gridPosition, delegate
				{
					isWaitingForAction = false;
				});
				yield return new WaitUntil(() => !isWaitingForAction);
				yield return new WaitForSeconds(0.3f);
			}
			else
			{
				isActionTaking = false;
			}
		}
		yield return new WaitForSeconds(0.2f);
		state = State.Idle;
		TurnSystem.Instance.NextTurn();
	}

	private IEnumerator CalculateBestAIActionRoutine(Unit enemyUnit, Action<BaseAction, EnemyAIAction> callback)
	{
		BaseAction bestBaseAction = null;
		EnemyAIAction bestEnemyAIAction = null;
		if (enemyUnit == null)
		{
			callback?.Invoke(null, null);
			yield break;
		}
		BaseAction[] baseActionsArray = enemyUnit.GetBaseActionsArray();
		foreach (BaseAction baseAction in baseActionsArray)
		{
			if (enemyUnit.CanSpendActionPointsToTakeAction(baseAction))
			{
				EnemyAIAction bestEnemyAIAction2 = baseAction.GetBestEnemyAIAction();
				if (bestEnemyAIAction2 != null && (bestEnemyAIAction == null || bestEnemyAIAction2.actionValue > bestEnemyAIAction.actionValue))
				{
					bestEnemyAIAction = bestEnemyAIAction2;
					bestBaseAction = baseAction;
				}
				yield return null;
			}
		}
		callback?.Invoke(bestBaseAction, bestEnemyAIAction);
	}
}
