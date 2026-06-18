using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자신 주변에 공격력 강화 오라를 생성하는 액션.
/// 오라가 활성화된 동안 이 유닛의 턴이 끝날 때마다 범위 내 아군에게 공격력 버프를 부여한다.
/// BarrierAuraAction과 동일한 턴 종료 감지 패턴을 사용한다.
/// </summary>
public class AttackAuraAction : BaseAction
{
    [Header("Aura")]
    [SerializeField] private int auraRange = 2;
    [SerializeField] private int auraDuration = 3;      // 오라 지속 턴 수

    [Header("Buff")]
    [SerializeField] private int attackBonus = 10;      // 매 턴 종료 시 부여할 공격력 버프 수치
    [SerializeField] private int buffDuration = 1;      // 부여된 버프의 지속 턴

    private bool isAuraActive = false;
    private int turnsRemaining = 0;

    // 이 유닛의 턴이 시작되면 true, 다음 OnTurnChanged가 왔을 때 true이면 턴이 끝난 것
    private bool isTurnActive = false;

    public event EventHandler OnAuraActivated;
    public event EventHandler OnAuraDeactivated;
    public event EventHandler OnAuraTick;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }

    private void Start()
    {
        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
    }

    private void OnDestroy()
    {
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
    }

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        if (!isAuraActive) return;

        // 이 유닛의 턴이 끝난 시점 감지
        if (isTurnActive && TurnSystem.Instance.GetTurnUnit() != unit)
        {
            isTurnActive = false;
            ApplyAuraTick();

            turnsRemaining--;
            if (turnsRemaining <= 0)
                DeactivateAura();
        }

        if (TurnSystem.Instance.GetTurnUnit() == unit)
            isTurnActive = true;
    }

    /// <summary>범위 내 아군에게 공격력 버프를 부여한다.</summary>
    private void ApplyAuraTick()
    {
        GridPosition unitPos = unit.GetGridPosition();

        foreach (Unit ally in UnitManager.Instance.GetFriendlyUnitList())
        {
            if (ally == null) continue;

            GridPosition allyPos = ally.GetGridPosition();
            int dist = Mathf.Abs(allyPos.x - unitPos.x) + Mathf.Abs(allyPos.z - unitPos.z);
            if (dist > auraRange) continue;

            AttackBuffSystem abs = ally.GetComponent<AttackBuffSystem>();
            if (abs != null)
                abs.ApplyBuff(attackBonus, buffDuration);
        }

        OnAuraTick?.Invoke(this, EventArgs.Empty);
    }

    private void DeactivateAura()
    {
        isAuraActive = false;
        isTurnActive = false;
        OnAuraDeactivated?.Invoke(this, EventArgs.Empty);
    }

    public override string GetActionName() => "AttackAura";

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        ActionStart(onActionComplete);
        StartCoroutine(AuraRoutine());
    }

    private IEnumerator AuraRoutine()
    {
        turnsRemaining = auraDuration;
        isAuraActive = true;
        isTurnActive = true;

        OnAuraActivated?.Invoke(this, EventArgs.Empty);

        yield return new WaitForSeconds(0.3f);

        ActionComplete();
    }

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        return new List<GridPosition> { unit.GetGridPosition() };
    }

    public override List<GridPosition> GetActionRangeGridPositionList()
    {
        List<GridPosition> rangeList = new List<GridPosition>();
        GridPosition unitPos = unit.GetGridPosition();

        for (int x = -auraRange; x <= auraRange; x++)
        {
            for (int z = -auraRange; z <= auraRange; z++)
            {
                if (Mathf.Sqrt(x * x + z * z) > auraRange) continue;

                GridPosition testPos = unitPos + new GridPosition(x, z, 0);
                if (LevelGrid.Instance.IsValidGridPosition(testPos))
                    rangeList.Add(testPos);
            }
        }
        return rangeList;
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        GridPosition unitPos = unit.GetGridPosition();
        int nearbyAllies = 0;
        foreach (Unit ally in UnitManager.Instance.GetFriendlyUnitList())
        {
            if (ally == unit) continue;
            GridPosition allyPos = ally.GetGridPosition();
            int dist = Mathf.Abs(allyPos.x - unitPos.x) + Mathf.Abs(allyPos.z - unitPos.z);
            if (dist <= auraRange) nearbyAllies++;
        }
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 50 + nearbyAllies * 30 };
    }

    public bool IsAuraActive() => isAuraActive;
    public int GetTurnsRemaining() => turnsRemaining;
    public int GetAuraRange() => auraRange;
}
