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
}
