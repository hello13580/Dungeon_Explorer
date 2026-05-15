using System;
using UnityEngine;

public class ActionBusyUI : MonoBehaviour
{
	[SerializeField]
	private GameObject actionBusyUI;

	private void Start()
	{
		UnitActionSystem.Instance.OnBusyChanged += UnitActionSystem_OnBusyChanged;
		TurnSystem.Instance.OnTurnStarted += TurnSystem_OnTurnStarted;
	}

	private void UnitActionSystem_OnBusyChanged(object sender, bool isBusy)
	{
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
