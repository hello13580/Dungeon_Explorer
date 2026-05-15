using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UnitActionSystemUI : MonoBehaviour
{
	[SerializeField]
	private GameObject actionButtonPrefab;

	[SerializeField]
	private Transform actionButtonContainerGameObject;

	[SerializeField]
	private TextMeshProUGUI actionPointText;

	private List<ActionButtonUI> activeButtonList;

	private void Awake()
	{
		activeButtonList = new List<ActionButtonUI>();
	}

	private void Start()
	{
		CreateUnitActionButtons();
		UnitActionSystem.Instance.OnSelectedUnitChanged += UnitActionSystem_OnselectedUnitChanged;
		UnitActionSystem.Instance.OnSelectedActionChanged += UnitActionSystem_OnSelectedActionChanged;
		Unit.OnAnyActionPointsChanged += Unit_OnAnyActionPointsChanged;
		UpdateActionPointTxt();
	}

	private void Update()
	{
	}

	private void CreateUnitActionButtons()
	{
		foreach (Transform item in actionButtonContainerGameObject)
		{
			Destroy(item.gameObject);
		}
		activeButtonList.Clear();
		Unit selectedUnit = UnitActionSystem.Instance.GetSelectedUnit();
		if (selectedUnit != null)
		{
			BaseAction[] baseActionsArray = selectedUnit.GetBaseActionsArray();
			foreach (BaseAction baseAction in baseActionsArray)
			{
				ActionButtonUI component = Instantiate(actionButtonPrefab, actionButtonContainerGameObject).GetComponent<ActionButtonUI>();
				component.SetBaseAction(baseAction);
				activeButtonList.Add(component);
			}
		}
	}

	private void UnitActionSystem_OnselectedUnitChanged(object sender, Unit selectedUnit)
	{
		if (selectedUnit.GetTeamType() == TeamType.Player && UnitActionSystem.Instance.IsSelectedUnitTurn())
		{
			CreateUnitActionButtons();
		}
		UpdateSelectedVisual();
		UpdateActionPointTxt();
	}

	private void UnitActionSystem_OnSelectedActionChanged(object sender, BaseAction baseAction)
	{
		UpdateSelectedVisual();
	}

	private void Unit_OnAnyActionPointsChanged(object sender, EventArgs empty)
	{
		UpdateActionPointTxt();
	}

	private void UpdateSelectedVisual()
	{
		foreach (ActionButtonUI activeButton in activeButtonList)
		{
			activeButton.UpdateSelectedVisual();
		}
	}

	private void UpdateActionPointTxt()
	{
		Unit selectedUnit = UnitActionSystem.Instance.GetSelectedUnit();
		if (selectedUnit != null && selectedUnit.GetTeamType() == TeamType.Player)
		{
			actionPointText.text = "ActionPoints : " + selectedUnit.GetCurrentActionPoint();
		}
		else
		{
			actionPointText.text = "";
		}
	}
}
