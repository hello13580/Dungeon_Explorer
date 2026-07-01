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

		// StageManager가 있으면 초기 턴 시작을 StageManager에 위임
		// 없으면 씬에 직접 배치된 유닛으로 바로 시작
		if (StageManager.Instance == null && unitList.Count > 0)
		{
			unitList.Sort((Unit a, Unit b) => b.GetCurrentSpeed().CompareTo(a.GetCurrentSpeed()));
			NextTurn();
		}
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
			// [버그 수정] currentTurnUnit을 먼저 null로 클리어한 뒤 NextTurn()을 호출한다.
			// Unit.RegisterForNewStage()의 이벤트 중복 구독 버그가 수정됐으나,
			// 다른 경로로 OnAnyUnitDead가 중복 발생하더라도 두 번째 호출에서
			// unit == currentTurnUnit(null) 조건이 false가 되어 NextTurn()이 한 번만 실행된다.
			currentTurnUnit = null;
			NextTurn();
		}
	}

	/// <summary>
	/// 스테이지 전환 시 호출. 턴 카운터와 내부 상태를 초기화한다.
	/// </summary>
	public void ResetTurn()
	{
		turnNumber = 0;
		lastIndex = 0;
		currentTurnUnit = null;
		isPlayerTurn = true; // false면 UI가 "Enemy Turn 0"을 잘못 표시하므로 true로 초기화
	}

	/// <summary>
	/// StageManager가 유닛 스폰을 마친 뒤 호출. 유닛 목록을 정렬하고 첫 턴을 시작한다.
	/// </summary>
	public void StartStage()
	{
		// unitList는 UnitManager와 동일한 참조 — 새로 스폰된 유닛들이 이미 들어있음
		unitList.Sort((Unit a, Unit b) => b.GetCurrentSpeed().CompareTo(a.GetCurrentSpeed()));
		NextTurn();
	}
}
