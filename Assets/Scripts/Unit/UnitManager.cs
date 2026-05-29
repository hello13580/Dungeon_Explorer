using System;
using System.Collections.Generic;
using UnityEngine;

public class UnitManager : MonoBehaviour
{
	private List<Unit> unitList;

	private List<Unit> friendlyUnitList;

	private List<Unit> enemyUnitList;

	private List<Unit> neutralUnitList;

	public static UnitManager Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Debug.LogError("There's more than one UnitManager! " + transform + " - " + Instance);
			Destroy(gameObject);
			return;
		}
		Instance = this;
		DontDestroyOnLoad(gameObject);
		unitList = new List<Unit>();
		friendlyUnitList = new List<Unit>();
		enemyUnitList = new List<Unit>();
		neutralUnitList = new List<Unit>();
	}

	private void Start()
	{
		Unit.OnAnyUnitSpawned += Unit_OnAnyUnitSpawned;
		Unit.OnAnyUnitDead += Unit_OnAnyUnitDead;
	}

	private void Unit_OnAnyUnitSpawned(object sender, EventArgs empty)
	{
		Unit unit = sender as Unit;
		unitList.Add(unit);
		if (unit.GetTeamType() == TeamType.Player)
		{
			friendlyUnitList.Add(unit);
		}
		else if (unit.GetTeamType() == TeamType.Neutral)
		{
			neutralUnitList.Add(unit);
		}
		else
		{
			enemyUnitList.Add(unit);
		}
	}

	private void Unit_OnAnyUnitDead(object sender, EventArgs empty)
	{
		Unit unit = sender as Unit;
		unitList.Remove(unit);
		if (unit.GetTeamType() == TeamType.Player)
		{
			friendlyUnitList.Remove(unit);
		}
		else if (unit.GetTeamType() == TeamType.Neutral)
		{
			neutralUnitList.Remove(unit);
		}
		else
		{
			enemyUnitList.Remove(unit);
		}
	}

	public List<Unit> GetUnitList()
	{
		return unitList;
	}

	public List<Unit> GetFriendlyUnitList()
	{
		return friendlyUnitList;
	}

	public List<Unit> GetEnemyUnitList()
	{
		return enemyUnitList;
	}

	private List<Unit> GetNeutralUnitList()
	{
		return neutralUnitList;
	}

	public void RemoveUnit(int i)
	{
		unitList.RemoveAt(i);
	}

	/// <summary>
	/// 스테이지 전환 시 호출. 모든 유닛 GameObject를 파괴하고 리스트를 비운다.
	/// 리스트 객체 자체는 재할당하지 않는다 — TurnSystem이 같은 리스트 참조를 유지해야 하기 때문.
	/// </summary>
	public void ClearAllUnits()
	{
		// 파괴 대상을 먼저 복사 (Destroy 도중 리스트 변경 방지)
		List<Unit> unitsToDestroy = new List<Unit>(unitList);

		// 리스트 내용만 비움 (참조 객체는 유지)
		unitList.Clear();
		friendlyUnitList.Clear();
		enemyUnitList.Clear();
		neutralUnitList.Clear();

		// GameObject 파괴 (HealthSystem 사망 이벤트 체인을 타지 않도록 직접 Destroy)
		foreach (Unit unit in unitsToDestroy)
		{
			if (unit != null)
				Destroy(unit.gameObject);
		}
	}
}
