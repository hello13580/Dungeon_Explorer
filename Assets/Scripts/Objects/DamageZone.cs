using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장판형 지속 화상 구역.
/// - 배치 즉시: 이미 안에 있는 유닛에게 화상 부여
/// - 진입 시:   액션이 끝날 때 새로 들어온 유닛에게 화상 부여 (외부 → 내부 이동만 감지)
/// - 자기 턴:   해당 유닛의 턴이 시작될 때 장판 안에 있으면 화상 추가 부여
/// - 지속 턴:   시전자 팀 턴마다 카운트다운, 소진되면 제거
///
/// 실제 피해는 직접 주지 않고 StatusEffectType.Burn을 부여한다.
/// 화상 자체의 피해/감소는 StatusEffectSystem이 대상 유닛의 턴 종료 시 처리한다.
/// </summary>
public class DamageZone : MonoBehaviour
{
    public static event EventHandler OnAnyDamageZoneCreated;
    public static event EventHandler OnAnyDamageZoneDestroyed;

    private List<GridPosition> affectedPositions;
    private int enterBurnStacks;   // 진입 즉시 부여하는 화상 수치
    private int tickBurnStacks;    // 자기 턴 시작 시 추가로 부여하는 화상 수치
    private int turnsRemaining;
    private TeamType ownerTeamType;

    // 이전 액션 종료 시 장판 안에 있던 유닛 집합 — 이 안에 있으면 "원래부터 장판 안"으로 간주
    private HashSet<Unit> unitsInZoneLastFrame = new HashSet<Unit>();
    // 이번 액션 중 이동 이벤트로 이미 화상을 받은 유닛 — BaseAction_OnAnyActionEnded 중복 방지용
    private HashSet<Unit> unitsDamagedOnEntry = new HashSet<Unit>();

    public void Setup(List<GridPosition> positions, int initialBurnStacks, int tickBurnStacks, int duration, TeamType ownerTeamType, int attackPower = 0)
    {
        this.affectedPositions = positions;
        // 시전자 공격력을 진입/배치/틱 화상 수치 모두에 더함
        this.enterBurnStacks = initialBurnStacks + attackPower;
        this.tickBurnStacks = tickBurnStacks + attackPower;
        this.turnsRemaining = duration;
        this.ownerTeamType = ownerTeamType;

        // 배치 즉시 화상 부여 + 현재 안에 있는 유닛 초기 등록
        HashSet<Unit> initial = GetUnitsInZone();
        foreach (Unit u in initial)
            ApplyBurn(u, enterBurnStacks);
        unitsInZoneLastFrame = initial;

        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
        BaseAction.OnAnyActionEnded += BaseAction_OnAnyActionEnded;
        MoveAction.OnAnyUnitSteppedOnTile += MoveAction_OnAnyUnitSteppedOnTile;
        StageManager.OnStageLoadingStarted += StageManager_OnStageLoadingStarted;
        OnAnyDamageZoneCreated?.Invoke(this, EventArgs.Empty);
    }

    private void OnDestroy()
    {
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
        BaseAction.OnAnyActionEnded -= BaseAction_OnAnyActionEnded;
        MoveAction.OnAnyUnitSteppedOnTile -= MoveAction_OnAnyUnitSteppedOnTile;
        StageManager.OnStageLoadingStarted -= StageManager_OnStageLoadingStarted;
    }

    // 스테이지 전환 시 남아있는 장판을 즉시 제거한다
    private void StageManager_OnStageLoadingStarted(object sender, EventArgs e)
    {
        OnAnyDamageZoneDestroyed?.Invoke(this, EventArgs.Empty);
        Destroy(gameObject);
    }

    // ─── 진입 감지 ─────────────────────────────────────────────────────

    /// <summary>
    /// 이동 중 장판 타일을 밟는 즉시 호출된다.
    /// 액션 시작 전 장판 밖에 있던 유닛에게만 즉시 화상을 부여한다.
    /// </summary>
    private void MoveAction_OnAnyUnitSteppedOnTile(object sender, GridPosition steppedPos)
    {
        if (!affectedPositions.Contains(steppedPos)) return;
        if (sender is not MoveAction moveAction) return;
        Unit u = moveAction.GetUnit();
        if (!TeamHelper.IsHostile(ownerTeamType, u.GetTeamType())) return;
        // 원래부터 장판 안에 있던 유닛이거나 이미 이번 액션에서 화상을 받았으면 스킵
        if (unitsInZoneLastFrame.Contains(u)) return;
        if (unitsDamagedOnEntry.Contains(u)) return;

        ApplyBurn(u, enterBurnStacks);
        unitsDamagedOnEntry.Add(u);
    }

    /// <summary>
    /// 액션 종료 시 이동 이벤트를 통하지 않고 장판에 진입한 유닛을 처리한다.
    /// (이동 외 액션으로 순간이동하듯 장판 안에 들어온 경우 등)
    /// </summary>
    private void BaseAction_OnAnyActionEnded(object sender, EventArgs e)
    {
        HashSet<Unit> current = GetUnitsInZone();

        foreach (Unit u in current)
        {
            // 이미 이동 이벤트로 화상을 받았거나 원래부터 장판 안에 있던 유닛은 스킵
            if (unitsInZoneLastFrame.Contains(u)) continue;
            if (unitsDamagedOnEntry.Contains(u)) continue;

            ApplyBurn(u, enterBurnStacks);
        }

        unitsDamagedOnEntry.Clear();
        unitsInZoneLastFrame = current;
    }

    // ─── 턴 처리 ───────────────────────────────────────────────────────

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        Unit turnUnit = TurnSystem.Instance.GetTurnUnit();

        // 현재 턴 유닛이 장판 안에 있으면 화상 추가 부여
        if (turnUnit != null && IsUnitInZone(turnUnit)
            && TeamHelper.IsHostile(ownerTeamType, turnUnit.GetTeamType()))
        {
            ApplyBurn(turnUnit, tickBurnStacks);
        }

        // 시전자 팀 턴 시작마다 지속 턴 카운트다운
        bool isOwnerTurn = ownerTeamType == TeamType.Player
            ? TurnSystem.Instance.IsPlayerTurn()
            : !TurnSystem.Instance.IsPlayerTurn();

        if (!isOwnerTurn) return;

        turnsRemaining--;
        if (turnsRemaining <= 0)
        {
            OnAnyDamageZoneDestroyed?.Invoke(this, EventArgs.Empty);
            TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;

            ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>();
            if (particles.Length > 0)
            {
                foreach (ParticleSystem ps in particles)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

                float maxLifetime = 0f;
                foreach (ParticleSystem ps in particles)
                    maxLifetime = Mathf.Max(maxLifetime, ps.main.startLifetime.constantMax);

                Destroy(gameObject, maxLifetime);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    // ─── 유틸 ─────────────────────────────────────────────────────────

    /// <summary>직접 피해를 주는 대신 화상 상태이상을 부여한다. 같은 수치만큼 누적된다.</summary>
    private void ApplyBurn(Unit u, int stacks)
    {
        if (stacks <= 0) return;
        StatusEffectSystem ses = u.GetComponent<StatusEffectSystem>();
        if (ses == null) return;
        ses.AddEffect(new StatusEffect(StatusEffectType.Burn, stacks, stacks, "화상", StackingMode.AddValue));
    }

    private HashSet<Unit> GetUnitsInZone()
    {
        HashSet<Unit> result = new HashSet<Unit>();
        foreach (GridPosition pos in affectedPositions)
        {
            if (!LevelGrid.Instance.IsValidGridPosition(pos)) continue;
            foreach (Unit u in LevelGrid.Instance.GetUnitListAtGridPosition(pos))
            {
                if (TeamHelper.IsHostile(ownerTeamType, u.GetTeamType()))
                    result.Add(u);
            }
        }
        return result;
    }

    private bool IsUnitInZone(Unit u)
    {
        // 사이즈 N 유닛은 N×N 서브타일 중 하나라도 장판과 겹치면 피해 대상
        int size = u.GetSize();
        GridPosition origin = u.GetGridPosition();
        for (int i = 0; i < size; i++)
            for (int j = 0; j < size; j++)
                if (affectedPositions.Contains(new GridPosition(origin.x + i, origin.z + j, origin.floor)))
                    return true;
        return false;
    }

    public List<GridPosition> GetAffectedPositions() => affectedPositions;
    public int GetTurnsRemaining() => turnsRemaining;
}
