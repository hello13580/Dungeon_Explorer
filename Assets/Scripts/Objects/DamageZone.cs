using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장판형 지속 피해 구역.
/// - 배치 즉시: 이미 안에 있는 유닛에게 initialDamage
/// - 진입 시:   액션이 끝날 때마다 새로 들어온 유닛에게 즉시 enterDamage
/// - 자기 턴:   해당 유닛의 턴이 시작될 때 장판 안에 있으면 tickDamage
/// - 지속 턴:   시전자 팀 턴마다 카운트다운, 소진되면 제거
/// </summary>
public class DamageZone : MonoBehaviour
{
    public static event EventHandler OnAnyDamageZoneCreated;
    public static event EventHandler OnAnyDamageZoneDestroyed;

    private List<GridPosition> affectedPositions;
    private int enterDamage;   // 진입 즉시 피해 (= initialDamage 재활용)
    private int tickDamage;    // 자기 턴 시작 시 피해
    private int turnsRemaining;
    private TeamType ownerTeamType;

    // 이전 프레임에 장판 안에 있던 유닛 집합 — 새 진입 감지용
    private HashSet<Unit> unitsInZoneLastFrame = new HashSet<Unit>();

    public void Setup(List<GridPosition> positions, int initialDamage, int tickDamage, int duration, TeamType ownerTeamType, int attackPower = 0)
    {
        this.affectedPositions = positions;
        // 시전자 공격력을 진입/배치/틱 피해 모두에 더함
        this.enterDamage = initialDamage + attackPower;
        this.tickDamage = tickDamage + attackPower;
        this.turnsRemaining = duration;
        this.ownerTeamType = ownerTeamType;

        // 배치 즉시 피해 + 현재 안에 있는 유닛 초기 등록 (enterDamage에 공격력이 이미 포함됨)
        HashSet<Unit> initial = GetUnitsInZone();
        foreach (Unit u in initial)
            u.Damage(enterDamage);
        unitsInZoneLastFrame = initial;

        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
        BaseAction.OnAnyActionEnded += BaseAction_OnAnyActionEnded;
        OnAnyDamageZoneCreated?.Invoke(this, EventArgs.Empty);
    }

    private void OnDestroy()
    {
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
        BaseAction.OnAnyActionEnded -= BaseAction_OnAnyActionEnded;
    }

    // ─── 진입 감지 ─────────────────────────────────────────────────────

    private void BaseAction_OnAnyActionEnded(object sender, EventArgs e)
    {
        HashSet<Unit> current = GetUnitsInZone();

        foreach (Unit u in current)
        {
            // 이전에 없었던 유닛 = 새로 진입한 유닛
            if (!unitsInZoneLastFrame.Contains(u))
                u.Damage(enterDamage);
        }

        unitsInZoneLastFrame = current;
    }

    // ─── 턴 처리 ───────────────────────────────────────────────────────

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        Unit turnUnit = TurnSystem.Instance.GetTurnUnit();

        // 현재 턴 유닛이 장판 안에 있으면 틱 피해
        if (turnUnit != null && IsUnitInZone(turnUnit)
            && TeamHelper.IsHostile(ownerTeamType, turnUnit.GetTeamType()))
        {
            turnUnit.Damage(tickDamage);
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
        return affectedPositions.Contains(u.GetGridPosition());
    }

    public List<GridPosition> GetAffectedPositions() => affectedPositions;
    public int GetTurnsRemaining() => turnsRemaining;
}
