using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장판형 지속 피해 구역. BlizzardZone과 동일한 생명주기 구조를 쓰되,
/// 둔화(MovementReduce) 뿐 아니라 약화(DamageReduce)까지 같이 적용한다.
/// - 배치 즉시: 이미 안에 있는 유닛에게 initialDamage + 둔화 + 약화
/// - 진입 시:   액션이 끝날 때 새로 들어온 유닛에게 initialDamage + 둔화 + 약화
/// - 자기 턴:   해당 유닛의 턴이 시작될 때 장판 안에 있으면 tickDamage + 둔화/약화 갱신
/// - 지속 턴:   시전자 팀 턴마다 카운트다운, 소진되면 제거
/// </summary>
public class PlagueZone : MonoBehaviour
{
    public static event EventHandler OnAnyPlagueZoneCreated;
    public static event EventHandler OnAnyPlagueZoneDestroyed;

    /// <summary>이 장판 인스턴스가 지속 턴을 다 써서 끝날 때 발생. 시전 액션의 아웃라인 종료 등에 사용.</summary>
    public event EventHandler OnZoneEnded;

    private List<GridPosition> affectedPositions;
    private int enterDamage;
    private int tickDamage;
    private int turnsRemaining;
    private TeamType ownerTeamType;
    private float slowValue;
    private int slowDuration;
    private float weakenValue;
    private int weakenDuration;

    private HashSet<Unit> unitsInZoneLastFrame = new HashSet<Unit>();
    private HashSet<Unit> unitsDamagedOnEntry = new HashSet<Unit>();

    public void Setup(List<GridPosition> positions, int initialDamage, int tickDamage, int duration,
                       TeamType ownerTeamType, float slowValue, int slowDuration,
                       float weakenValue, int weakenDuration, int attackPower = 0)
    {
        this.affectedPositions = positions;
        this.enterDamage = initialDamage + attackPower;
        this.tickDamage = tickDamage + attackPower;
        this.turnsRemaining = duration;
        this.ownerTeamType = ownerTeamType;
        this.slowValue = slowValue;
        this.slowDuration = slowDuration;
        this.weakenValue = weakenValue;
        this.weakenDuration = weakenDuration;

        HashSet<Unit> initial = GetUnitsInZone();
        foreach (Unit u in initial)
            ApplyHit(u, enterDamage);
        unitsInZoneLastFrame = initial;

        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
        BaseAction.OnAnyActionEnded += BaseAction_OnAnyActionEnded;
        MoveAction.OnAnyUnitSteppedOnTile += MoveAction_OnAnyUnitSteppedOnTile;
        StageManager.OnStageLoadingStarted += StageManager_OnStageLoadingStarted;
        OnAnyPlagueZoneCreated?.Invoke(this, EventArgs.Empty);
    }

    private void OnDestroy()
    {
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
        BaseAction.OnAnyActionEnded -= BaseAction_OnAnyActionEnded;
        MoveAction.OnAnyUnitSteppedOnTile -= MoveAction_OnAnyUnitSteppedOnTile;
        StageManager.OnStageLoadingStarted -= StageManager_OnStageLoadingStarted;
    }

    private void StageManager_OnStageLoadingStarted(object sender, EventArgs e)
    {
        OnAnyPlagueZoneDestroyed?.Invoke(this, EventArgs.Empty);
        Destroy(gameObject);
    }

    // ─── 진입 감지 ─────────────────────────────────────────────────────

    private void MoveAction_OnAnyUnitSteppedOnTile(object sender, GridPosition steppedPos)
    {
        if (!affectedPositions.Contains(steppedPos)) return;
        if (sender is not MoveAction moveAction) return;
        Unit u = moveAction.GetUnit();
        if (!TeamHelper.IsHostile(ownerTeamType, u.GetTeamType())) return;
        if (unitsInZoneLastFrame.Contains(u)) return;
        if (unitsDamagedOnEntry.Contains(u)) return;

        HitReactionSystem hrs = u.GetComponent<HitReactionSystem>();
        if (hrs != null) { hrs.SetHitForce(0f); hrs.SetHitDirection(Vector3.zero); }
        ApplyHit(u, enterDamage);
        unitsDamagedOnEntry.Add(u);
    }

    private void BaseAction_OnAnyActionEnded(object sender, EventArgs e)
    {
        HashSet<Unit> current = GetUnitsInZone();

        foreach (Unit u in current)
        {
            if (unitsInZoneLastFrame.Contains(u)) continue;
            if (unitsDamagedOnEntry.Contains(u)) continue;

            HitReactionSystem hrs = u.GetComponent<HitReactionSystem>();
            if (hrs != null) { hrs.SetHitForce(0f); hrs.SetHitDirection(Vector3.zero); }
            ApplyHit(u, enterDamage);
        }

        unitsDamagedOnEntry.Clear();
        unitsInZoneLastFrame = current;
    }

    // ─── 턴 처리 ───────────────────────────────────────────────────────

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        Unit turnUnit = TurnSystem.Instance.GetTurnUnit();

        if (turnUnit != null && IsUnitInZone(turnUnit)
            && TeamHelper.IsHostile(ownerTeamType, turnUnit.GetTeamType()))
        {
            HitReactionSystem hrs = turnUnit.GetComponent<HitReactionSystem>();
            if (hrs != null) { hrs.SetHitForce(0f); hrs.SetHitDirection(Vector3.zero); }
            ApplyHit(turnUnit, tickDamage);
        }

        bool isOwnerTurn = ownerTeamType == TeamType.Player
            ? TurnSystem.Instance.IsPlayerTurn()
            : !TurnSystem.Instance.IsPlayerTurn();

        if (!isOwnerTurn) return;

        turnsRemaining--;
        if (turnsRemaining <= 0)
        {
            OnAnyPlagueZoneDestroyed?.Invoke(this, EventArgs.Empty);
            OnZoneEnded?.Invoke(this, EventArgs.Empty);
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

    private void ApplyHit(Unit u, int damageAmount)
    {
        if (damageAmount > 0)
            u.Damage(damageAmount);

        StatusEffectSystem ses = u.GetComponent<StatusEffectSystem>();
        if (ses == null) return;

        ses.AddEffect(new StatusEffect(
            StatusEffectType.MovementReduce, slowValue, slowDuration,
            "둔화", StackingMode.RefreshDuration));

        ses.AddEffect(new StatusEffect(
            StatusEffectType.DamageReduce, weakenValue, weakenDuration,
            "약화", StackingMode.RefreshDuration));
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
