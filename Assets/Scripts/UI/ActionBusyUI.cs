using System;
using UnityEngine;

public class ActionBusyUI : MonoBehaviour
{
	[SerializeField]
	private GameObject actionBusyUI;

	private bool isStageClear = false;

	private void Start()
	{
		UnitActionSystem.Instance.OnBusyChanged += UnitActionSystem_OnBusyChanged;
		TurnSystem.Instance.OnTurnStarted += TurnSystem_OnTurnStarted;
		UnitManager.OnStageClear += UnitManager_OnStageClear;
		// 새 스테이지가 로드되면 isStageClear를 리셋해 UI가 다시 작동하도록 한다
		StageManager.OnStageLoaded += StageManager_OnStageLoaded;
	}

	private void OnDestroy()
	{
		UnitManager.OnStageClear -= UnitManager_OnStageClear;
		StageManager.OnStageLoaded -= StageManager_OnStageLoaded;
	}

	private void StageManager_OnStageLoaded(object sender, System.EventArgs e)
	{
		// 스테이지가 새로 로드됐으므로 클리어 플래그 초기화
		isStageClear = false;
	}

	private void UnitManager_OnStageClear(object sender, EventArgs e)
	{
		isStageClear = true;
		Hide();
	}

	private void UnitActionSystem_OnBusyChanged(object sender, bool isBusy)
	{
		if (isStageClear) return;

		if (!isBusy && UnitActionSystem.Instance.IsSelectedUnitTurn() && TurnSystem.Instance.IsPlayerTurn())
		{
			Show();
		}
		else
		{
			Hide();
		}
	}

	private void TurnSystem_OnTurnStarted(object sender, EventArgs empty)
	{
		if (isStageClear) return;

		if (UnitActionSystem.Instance.IsSelectedUnitTurn() && TurnSystem.Instance.IsPlayerTurn())
		{
			Show();
		}
		else
		{
			Hide();
		}
	}

	private void Show()
	{
		actionBusyUI.SetActive(true);
	}

	private void Hide()
	{
		actionBusyUI.SetActive(false);
	}
}
