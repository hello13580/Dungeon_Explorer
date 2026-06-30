using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitActionSystemUI : MonoBehaviour
{
	[Header("액션 버튼")]
	[SerializeField] private GameObject actionButtonPrefab;
	[SerializeField] private Transform actionButtonContainerGameObject;

	[Header("적 정보 패널")]
	[SerializeField] private GameObject enemyInfoPanel;
	[SerializeField] private TextMeshProUGUI enemyInfoText;

	[Header("현재 유닛으로 돌아가기")]
	[SerializeField] private Button returnToCurrentUnitButton;

	private List<ActionButtonUI> activeButtonList;

	private void Awake()
	{
		activeButtonList = new List<ActionButtonUI>();
	}

	private void Start()
	{
		if (enemyInfoPanel != null) enemyInfoPanel.SetActive(false);
		if (returnToCurrentUnitButton != null)
		{
			returnToCurrentUnitButton.gameObject.SetActive(false);
			returnToCurrentUnitButton.onClick.AddListener(OnReturnToCurrentUnitClicked);
		}

		CreateUnitActionButtons();
		UnitActionSystem.Instance.OnSelectedUnitChanged += UnitActionSystem_OnselectedUnitChanged;
		UnitActionSystem.Instance.OnSelectedActionChanged += UnitActionSystem_OnSelectedActionChanged;
		Unit.OnAnySkillsChanged += Unit_OnAnySkillsChanged;
		TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
	}

	private void OnDestroy()
	{
		if (UnitActionSystem.Instance != null)
		{
			UnitActionSystem.Instance.OnSelectedUnitChanged -= UnitActionSystem_OnselectedUnitChanged;
			UnitActionSystem.Instance.OnSelectedActionChanged -= UnitActionSystem_OnSelectedActionChanged;
		}
		Unit.OnAnySkillsChanged -= Unit_OnAnySkillsChanged;
		if (TurnSystem.Instance != null)
			TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
	}

	// 적을 본 채로 턴이 넘어가면(플레이어 턴 → 적 턴) 버튼 표시 여부를 다시 평가해야 한다.
	// 선택된 유닛이 바뀌지 않으면 UnitActionSystem_OnselectedUnitChanged가 호출되지 않으므로
	// 턴 변경 시점에 별도로 갱신한다.
	private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
	{
		Unit selectedUnit = UnitActionSystem.Instance.GetSelectedUnit();
		bool isEnemySelected = selectedUnit != null && selectedUnit.GetTeamType() != TeamType.Player;
		if (returnToCurrentUnitButton != null)
			returnToCurrentUnitButton.gameObject.SetActive(isEnemySelected && TurnSystem.Instance.IsPlayerTurn());
	}

	private void CreateUnitActionButtons()
	{
		foreach (Transform item in actionButtonContainerGameObject)
			Destroy(item.gameObject);
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
		bool isEnemy = selectedUnit != null && selectedUnit.GetTeamType() != TeamType.Player;
		bool isPlayerTurn = TurnSystem.Instance.IsPlayerTurn();

		if (isEnemy)
		{
			// 액션 버튼 숨기고 적 정보 표시
			actionButtonContainerGameObject.gameObject.SetActive(false);
			if (enemyInfoPanel != null)
			{
				enemyInfoPanel.SetActive(true);
				if (enemyInfoText != null)
					enemyInfoText.text = BuildEnemyInfo(selectedUnit);
			}
			// 플레이어 턴이면 돌아가기 버튼 표시
			if (returnToCurrentUnitButton != null)
				returnToCurrentUnitButton.gameObject.SetActive(isPlayerTurn);
		}
		else
		{
			// 아군 선택: 액션 버튼 표시
			actionButtonContainerGameObject.gameObject.SetActive(true);
			if (enemyInfoPanel != null) enemyInfoPanel.SetActive(false);
			if (returnToCurrentUnitButton != null) returnToCurrentUnitButton.gameObject.SetActive(false);

			if (selectedUnit != null && selectedUnit.GetTeamType() == TeamType.Player && UnitActionSystem.Instance.IsSelectedUnitTurn())
				CreateUnitActionButtons();
		}

		UpdateSelectedVisual();
	}

	private string BuildEnemyInfo(Unit unit)
	{
		HealthSystem hs = unit.GetComponent<HealthSystem>();
		string hp = hs != null ? $"{hs.GetCurrentHealth()} / {hs.GetMaxHealth()}" : "-";
		string info = $"{unit.GetUnitName()}\nHP  {hp}";
		string desc = unit.GetEnemyDescription();
		if (!string.IsNullOrEmpty(desc))
			info += $"\n\n{desc}";
		return info;
	}

	private void OnReturnToCurrentUnitClicked()
	{
		Unit turnUnit = TurnSystem.Instance.GetTurnUnit();
		if (turnUnit != null)
			UnitActionSystem.Instance.SetSelectedUnit(turnUnit);
	}

	private void UnitActionSystem_OnSelectedActionChanged(object sender, BaseAction baseAction)
	{
		UpdateSelectedVisual();
	}

	private void Unit_OnAnySkillsChanged(object sender, EventArgs empty)
	{
		CreateUnitActionButtons();
	}

	private void UpdateSelectedVisual()
	{
		foreach (ActionButtonUI activeButton in activeButtonList)
			activeButton.UpdateSelectedVisual();
	}
}
