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
	}

	private void OnDestroy()
	{
		UnitManager.OnStageClear -= UnitManager_OnStageClear;
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
