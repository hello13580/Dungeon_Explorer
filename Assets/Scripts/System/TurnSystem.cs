using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurnSystem : MonoBehaviour
{
	private List<Unit> unitList;

	private List<Unit> enemyUnitList;

	private int turnNumber;

	private int lastIndex;

	[SerializeField]
	private Unit currentTurnUnit;

	[SerializeField]
	private float actionGaugeRequired;

	[SerializeField]
	private bool isPlayerTurn;

	public static TurnSystem Instance { get; private set; }

	public event EventHandler OnTurnChanged;

	public event EventHandler OnTurnStarted;

	private void Awake()
	{
		turnNumber = 0;
		actionGaugeRequired = 50f;
		unitList = new List<Unit>();
		enemyUnitList = new List<Unit>();
		if (Instance != null && Instance != this)
		{
			Debug.LogError("There's more than one TurnSystem! " + transform + " - " + Instance);
			Destroy(gameObject);
		}
		else
		{
			Instance = this;
			DontDestroyOnLoad(gameObject);
		}
	}

	private void Start()
	{
		Unit.OnAnyUnitDead += Unit_OnAnyUnitDead;
		unitList = UnitManager.Instance.GetUnitList();
		enemyUnitList = UnitManager.Instance.GetEnemyUnitList();
		unitList.Sort((Unit a, Unit b) => b.GetCurrentSpeed().CompareTo(a.GetCurrentSpeed()));
		NextTurn();
	}

	private void StartTurn()
	{
		if (unitList.Count > 0)
		{
			while (true)
			{
				for (int i = 0; i < unitList.Count; i++)
				{
					int index = (lastIndex + i) % unitList.Count;
					Unit unit = unitList[index];
					if (unit.CanAct(actionGaugeRequired))
					{
						lastIndex = i + 1;
						currentTurnUnit = unit;
						UnitActionSystem.Instance.SetSelectedUnit(currentTurnUnit);
						Debug.Log("유닛 배정");
						SetTurn(currentTurnUnit.GetTeamType());
						currentTurnUnit.SubGauge(actionGaugeRequired);
						return;
					}
				}
				foreach (Unit unit2 in unitList)
				{
					unit2.AddGauge();
				}
			}
		}
		Instance.RoundEnd();
	}

	public int GetTurnNumber()
	{
		return turnNumber;
	}

	public void NextTurn()
	{
		StartCoroutine(NextTurnRoutine());
	}

	private IEnumerator NextTurnRoutine()
	{
		yield return null;
		turnNumber++;
		StartTurn();
		this.OnTurnStarted?.Invoke(this, EventArgs.Empty);
		this.OnTurnChanged?.Invoke(this, EventArgs.Empty);
	}

	public bool IsPlayerTurn()
	{
		return isPlayerTurn;
	}

	private void SetTurn(TeamType teamType)
	{
		isPlayerTurn = teamType == TeamType.Player;
	}

	public void RoundEnd()
	{
		Debug.Log("라운드가 끝났습니다");
	}

	public Unit GetTurnUnit()
	{
		return currentTurnUnit;
	}

	public void Unit_OnAnyUnitDead(object sender, EventArgs empty)
	{
		if (sender is Unit unit && unit == currentTurnUnit)
		{
			NextTurn();
		}
	}
}
