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
		// [버그 수정] 스테이지 전환 시 진행 중인 적 턴 코루틴을 강제 종료한다.
		// 수정 전: 마지막 적이 MoveAction 10초 타임아웃 대기 중에 스테이지가 클리어되면
		//   OnEnemyTurnRoutine 코루틴이 살아있는 채로 다음 스테이지가 시작됐다.
		//   타임아웃이 새 스테이지의 첫 플레이어 턴 도중에 터지면서 NextTurn()을 강제 호출,
		//   턴 시스템이 꼬여 모든 아군이 조작 불가 상태가 됐다.
		UnitManager.OnStageClear += UnitManager_OnStageClear;
		StageManager.OnStageLoadingStarted += StageManager_OnStageLoadingStarted;
	}

	private void OnDestroy()
	{
		UnitManager.OnStageClear -= UnitManager_OnStageClear;
		StageManager.OnStageLoadingStarted -= StageManager_OnStageLoadingStarted;
	}

	// 적 전멸(스테이지 클리어) 시 호출 — 진행 중인 적 AI 코루틴을 즉시 중단한다.
	private void UnitManager_OnStageClear(object sender, EventArgs e)
	{
		StopAllCoroutines();
		state = State.Idle;
		currentUnit = null;
	}

	// 새 스테이지 로드 시작 시 호출 — 이전 스테이지의 잔여 코루틴을 중단한다.
	// OnStageClear보다 늦게 발생하지만 이중으로 처리해 어떤 경로로든 전환이 일어나도 안전하게 정리한다.
	private void StageManager_OnStageLoadingStarted(object sender, EventArgs e)
	{
		StopAllCoroutines();
		state = State.Idle;
		currentUnit = null;
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
			// null 체크만 유지. AP가 0이어도 이동(0 코스트)이 남아있을 수 있으므로
			// AP로 루프를 조기 종료하지 않는다.
			// 실행 가능한 액션이 없으면 아래 else 블록에서 isActionTaking = false로 자연 종료된다.
			if (currentUnit == null)
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
