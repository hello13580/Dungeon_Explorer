using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자신 주변에 공격력 강화 오라를 생성하는 액션.
/// 범위 안에 있는 아군은 즉시 고정 공격력 버프를 받고, 범위 밖으로 나가거나 오라가 꺼지면 버프가 사라진다.
/// </summary>
public class AttackAuraAction : BaseAction, IAuraAction
{
    protected override string DefaultActionName() => "공격 오라";
    [Header("Aura")]
    [SerializeField] private int auraRange = 2;
    [SerializeField] private int auraDuration = 3;   // 오라 지속 턴 수

    [Header("Buff")]
    [SerializeField] private int attackBonus = 10;   // 범위 안에 있는 동안 유지되는 공격력 보너스

    private bool isAuraActive = false;
    private int turnsRemaining = 0;

    // 턴 종료 감지용 플래그
    private bool isTurnActive = false;

    // 현재 오라 버프를 받고 있는 유닛 목록 — 범위 이탈 감지에 사용
    private HashSet<Unit> buffedUnits = new HashSet<Unit>();

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
        // 이동 등 액션이 끝날 때마다 범위 진입/이탈을 갱신
        BaseAction.OnAnyActionEnded += BaseAction_OnAnyActionEnded;
        StageManager.OnStageLoadingStarted += StageManager_OnStageLoadingStarted;
    }

    private void OnDestroy()
    {
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
        BaseAction.OnAnyActionEnded -= BaseAction_OnAnyActionEnded;
        StageManager.OnStageLoadingStarted -= StageManager_OnStageLoadingStarted;
    }

    // 스테이지 전환 시 오라가 다음 스테이지까지 그대로 이어지지 않도록 강제 해제한다
    private void StageManager_OnStageLoadingStarted(object sender, EventArgs e)
    {
        if (isAuraActive) DeactivateAura();
    }

    private void BaseAction_OnAnyActionEnded(object sender, EventArgs e)
    {
        if (!isAuraActive) return;
        RefreshAuraBuffs();
    }

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        if (!isAuraActive) return;

        // 이 유닛의 턴이 끝난 시점 감지 — 오라 지속 턴 차감
        if (isTurnActive && TurnSystem.Instance.GetTurnUnit() != unit)
        {
            isTurnActive = false;
            turnsRemaining--;
            if (turnsRemaining <= 0)
                DeactivateAura();
        }

        if (TurnSystem.Instance.GetTurnUnit() == unit)
            isTurnActive = true;
    }

    /// <summary>범위 안의 아군에게 버프를 유지하고, 범위 밖으로 나간 아군의 버프를 제거한다.</summary>
    private void RefreshAuraBuffs()
    {
        GridPosition unitPos = unit.GetGridPosition();
        HashSet<Unit> currentInRange = new HashSet<Unit>();

        foreach (Unit ally in UnitManager.Instance.GetFriendlyUnitList())
        {
            if (ally == null) continue;
            GridPosition allyPos = ally.GetGridPosition();
            int dist = Mathf.Abs(allyPos.x - unitPos.x) + Mathf.Abs(allyPos.z - unitPos.z);
            if (dist > auraRange) continue;

            AttackBuffSystem abs = ally.GetComponent<AttackBuffSystem>();
            if (abs != null)
            {
                abs.SetAuraBuff(attackBonus); // 이미 같은 값이면 이벤트 미발생
                currentInRange.Add(ally);
            }
        }

        // 범위를 벗어난 유닛의 버프 제거
        foreach (Unit prev in buffedUnits)
        {
            if (prev == null) continue;
            if (!currentInRange.Contains(prev))
                prev.GetComponent<AttackBuffSystem>()?.ClearAuraBuff();
        }

        buffedUnits = currentInRange;
        OnAuraTick?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>오라 해제 시 모든 버프 즉시 제거.</summary>
    private void DeactivateAura()
    {
        foreach (Unit u in buffedUnits)
        {
            if (u == null) continue;
            u.GetComponent<AttackBuffSystem>()?.ClearAuraBuff();
        }
        buffedUnits.Clear();

        isAuraActive = false;
        isTurnActive = false;
        OnAuraDeactivated?.Invoke(this, EventArgs.Empty);
    }    public override string GetDescription() =>
        $"반경 {auraRange}칸 내 아군의 공격력을 {attackBonus} 증가시키는 오라를 생성한다. 범위 밖으로 나가면 즉시 해제. ({auraDuration}턴 지속)";


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
        // 활성화 즉시 범위 내 아군에게 버프 적용
        RefreshAuraBuffs();

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
