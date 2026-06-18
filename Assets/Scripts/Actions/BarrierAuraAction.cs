using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자신 주변에 오라를 생성하는 액션.
/// 오라가 활성화된 동안 이 유닛의 턴이 끝날 때마다 범위 내 아군에게 방어막을 부여한다.
/// 지정한 지속 턴이 지나면 오라가 해제된다.
/// </summary>
public class BarrierAuraAction : BaseAction, IAuraAction
{
    [Header("Aura")]
    [SerializeField] private int auraRange = 2;           // 방어막을 부여할 아군 탐색 범위 (칸)
    [SerializeField] private int auraDuration = 3;        // 오라 지속 턴 수

    [Header("Barrier")]
    [SerializeField] private int barrierAmount = 15;      // 매 턴 종료 시 부여할 방어막 수치
    [SerializeField] private int barrierDuration = 1;     // 부여된 방어막의 지속 턴

    // 오라 활성화 상태 및 남은 턴
    private bool isAuraActive = false;
    private int turnsRemaining = 0;

    // 턴 종료 감지용 플래그 — StatusEffectSystem의 Poison과 동일한 패턴
    // 이 유닛의 턴이 시작되면 true, 다음 OnTurnChanged가 왔을 때 true이면 턴이 끝난 것
    private bool isTurnActive = false;

    public event EventHandler OnAuraActivated;    // 오라 활성화 시 — VFX 재생용
    public event EventHandler OnAuraDeactivated;  // 오라 해제 시 — VFX 제거용
    public event EventHandler OnAuraTick;         // 턴 종료 시 방어막 부여 시 — VFX용

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

        // ── 턴 종료 감지 ─────────────────────────────────────────────
        // isTurnActive가 true인데 현재 턴이 이 유닛이 아니면 방금 이 유닛의 턴이 끝난 것
        if (isTurnActive && TurnSystem.Instance.GetTurnUnit() != unit)
        {
            isTurnActive = false;
            ApplyAuraTick();

            // 지속 턴 차감 — 0이 되면 오라 해제
            turnsRemaining--;
            if (turnsRemaining <= 0)
                DeactivateAura();
        }

        // ── 이 유닛의 턴 시작 감지 ───────────────────────────────────
        if (TurnSystem.Instance.GetTurnUnit() == unit)
            isTurnActive = true;
    }

    /// <summary>범위 내 아군에게 방어막을 부여한다.</summary>
    private void ApplyAuraTick()
    {
        GridPosition unitPos = unit.GetGridPosition();

        foreach (Unit ally in UnitManager.Instance.GetFriendlyUnitList())
        {
            if (ally == null) continue;

            GridPosition allyPos = ally.GetGridPosition();
            int dist = Mathf.Abs(allyPos.x - unitPos.x) + Mathf.Abs(allyPos.z - unitPos.z);
            if (dist > auraRange) continue;

            BarrierSystem bs = ally.GetComponent<BarrierSystem>();
            if (bs != null)
                // 고정 방어막 수치 + 시전자 방어력 스탯
                bs.ApplyBarrier(barrierAmount + unit.GetDefensePower(), barrierDuration);
        }

        OnAuraTick?.Invoke(this, EventArgs.Empty);
    }

    private void DeactivateAura()
    {
        isAuraActive = false;
        isTurnActive = false;
        OnAuraDeactivated?.Invoke(this, EventArgs.Empty);
    }

    public override string GetActionName() => "Aura";

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        ActionStart(onActionComplete);
        StartCoroutine(AuraRoutine());
    }

    private IEnumerator AuraRoutine()
    {
        // 이미 오라가 활성화 상태면 지속 턴을 갱신한다
        turnsRemaining = auraDuration;
        isAuraActive = true;
        // 오라는 이 유닛의 턴 중에 사용되므로 true — 현재 턴이 끝날 때 바로 방어막을 부여한다
        isTurnActive = true;

        OnAuraActivated?.Invoke(this, EventArgs.Empty);

        yield return new WaitForSeconds(0.3f);

        ActionComplete();
    }

    // 자기 자신 위치만 유효 타일
    public override List<GridPosition> GetValidActionGridPositionList()
    {
        // 이미 오라가 활성화 중이어도 재사용해서 지속 턴을 갱신할 수 있다
        return new List<GridPosition> { unit.GetGridPosition() };
    }

    public override List<GridPosition> GetActionRangeGridPositionList()
    {
        // 범위 시각화 — 오라가 영향을 미칠 원형 범위 표시
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
        // 아군 AI가 오라를 사용할 경우 — 주변 아군이 많을수록 우선순위 높임
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
