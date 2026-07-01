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

		// 적 턴 시작 시 해당 유닛으로 카메라를 이동한다.
		if (currentUnit != null && CameraController.Instance != null)
			CameraController.Instance.MoveToPosition(currentUnit.GetWorldPosition());

		yield return new WaitForSeconds(0.5f);
		bool isActionTaking = true;
		while (isActionTaking)
		{
			// 행동력이 0이면 AI 평가 자체를 건너뛰고 즉시 턴을 끝낸다.
			// 평가/실행 도중 어딘가에서 멈추는 케이스에 대한 최종 안전장치.
			if (currentUnit == null || currentUnit.GetCurrentActionPoint() <= 0)
			{
				isActionTaking = false;
				break;
			}

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
				// [버그 수정] WaitUntil → 타임아웃 루프로 교체.
				//   WaitUntil(() => !isWaitingForAction) 은 onActionComplete 콜백이 오지 않으면
				//   영원히 대기한다. 어떤 이유로든 액션이 완료 신호를 보내지 못하면
				//   (코루틴 예외 크래시, 애니메이션 이벤트 누락 등) 적의 턴이 끝나지 않는다.
				//   10초 타임아웃을 두어 최악의 경우에도 턴이 강제로 진행되도록 보장한다.
				float actionTimeout = 10f;
				while (isWaitingForAction && actionTimeout > 0f)
				{
					actionTimeout -= Time.deltaTime;
					yield return null;
				}
				if (isWaitingForAction)
					Debug.LogWarning($"[EnemyTurnManager] 액션 타임아웃: {bestBaseAction.GetType().Name} — 강제 진행");
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
			if (enemyUnit.CanTakeAction(baseAction))
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
