using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 얼음 구체를 발사해 대상에게 피해와 이동 거리 감소(둔화) 디버프를 적용하는 액션.
/// </summary>
public class IceOrbAction : BaseAction
{
    private enum State { Aiming, Shooting, Cooloff }

    public class OnShootEventArgs : EventArgs
    {
        public Unit targetUnit;
        public Unit shootingUnit;
    }

    [Header("Range")]
    [SerializeField] private int maxRange = 7;
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private Transform shootPointTransform;

    [Header("Damage")]
    [SerializeField] private int damage = 20;

    [Header("Slow Debuff")]
    [SerializeField] private float slowValue = 0.3f;     // 이동 거리 30% 감소
    [SerializeField] private int slowDuration = 2;       // 2턴 지속

    [Header("Projectile")]
    [SerializeField] private Transform iceOrbPrefab;

    [Header("Timing")]
    [SerializeField] private float rotateSpeed = 10f;

    private State state;
    private Unit targetUnit;
    private bool canShoot;

    private List<GridPosition> cachedValidList;
    private bool isCacheDirty = true;

    public event EventHandler<OnShootEventArgs> OnStartShooting;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }

    private void Start()
    {
        TurnSystem.Instance.OnTurnChanged += OnCacheInvalidated;
        BaseAction.OnAnyActionEnded += OnCacheInvalidated;
    }

    private void OnDestroy()
    {
        TurnSystem.Instance.OnTurnChanged -= OnCacheInvalidated;
        BaseAction.OnAnyActionEnded -= OnCacheInvalidated;
    }

    private void OnCacheInvalidated(object sender, EventArgs e) => isCacheDirty = true;

    public override string GetActionName() => "Ice Orb";

    // ─── 액션 실행 ─────────────────────────────────────────────────────

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        targetUnit = LevelGrid.Instance.GetUnitListAtGridPosition(gridPosition)[0];
        canShoot = true;
        state = State.Aiming;
        ActionStart(onActionComplete);
        StartCoroutine(ShootRoutine());
    }

    private IEnumerator ShootRoutine()
    {
        while (state == State.Aiming)
        {
            AimToTarget();
            yield return null;
        }

        yield return new WaitForSeconds(0.3f);

        if (canShoot && state == State.Shooting)
        {
            Shoot();
            canShoot = false;
        }

        yield return new WaitForSeconds(0.3f);
        state = State.Cooloff;

        yield return new WaitForSeconds(0.2f);
        ActionComplete();
    }

    private void AimToTarget()
    {
        Vector3 targetPos = targetUnit.GetWorldPosition();
        targetPos.y = transform.position.y;
        Vector3 aimDir = (targetPos - transform.position).normalized;

        transform.forward = Vector3.Slerp(transform.forward, aimDir, Time.deltaTime * rotateSpeed);

        if (Vector3.Angle(transform.forward, aimDir) < 1f)
            state = State.Shooting;
    }

    private void Shoot()
    {
        OnStartShooting?.Invoke(this, new OnShootEventArgs
        {
            targetUnit = targetUnit,
            shootingUnit = unit
        });

        Vector3 spawnPos = shootPointTransform != null
            ? shootPointTransform.position
            : unit.GetWorldPosition() + Vector3.up * (unit.GetCollider().bounds.size.y * 0.8f);

        Vector3 targetPos = targetUnit.GetWorldPosition()
                          + Vector3.up * (targetUnit.GetCollider().bounds.size.y * 0.5f);

        Transform orb = Instantiate(iceOrbPrefab, spawnPos, Quaternion.identity);
        IceOrbProjectile projectile = orb.GetComponent<IceOrbProjectile>();
        projectile.Setup(targetPos, targetUnit, damage, slowValue, slowDuration);
    }

    // ─── 유효 액션 위치 ────────────────────────────────────────────────

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        if (!isCacheDirty && cachedValidList != null) return cachedValidList;

        GridPosition unitPos = unit.GetGridPosition();
        int floorAmount = LevelGrid.Instance.GetFloorAmount();
        int minFloor = Mathf.Clamp(unitPos.floor - maxRange, 0, floorAmount - 1);
        int maxFloor = Mathf.Clamp(unitPos.floor + maxRange, 0, floorAmount - 1);

        cachedValidList = new List<GridPosition>();

        for (int x = -maxRange; x <= maxRange; x++)
        {
            for (int z = -maxRange; z <= maxRange; z++)
            {
                float sizeOffset = (unit.GetSize() - 1) * 0.5f;
                float dx = x - sizeOffset, dz = z - sizeOffset;
                if (Mathf.Sqrt(dx * dx + dz * dz) > maxRange) continue;

                for (int floor = minFloor; floor <= maxFloor; floor++)
                {
                    GridPosition testPos = new GridPosition(unitPos.x + x, unitPos.z + z, floor);

                    if (!LevelGrid.Instance.IsValidGridPosition(testPos)) continue;
                    if (!LevelGrid.Instance.IsGridPositionOccupied(testPos)) continue;

                    foreach (Unit target in LevelGrid.Instance.GetUnitListAtGridPosition(testPos))
                    {
                        if (!TeamHelper.IsHostile(unit.GetTeamType(), target.GetTeamType())) continue;
                        if (target.IsStealthed()) continue;
                        if (IsTargetVisible(target))
                            cachedValidList.Add(testPos);
                    }
                }
            }
        }

        isCacheDirty = false;
        return cachedValidList;
    }

    public override List<GridPosition> GetActionRangeGridPositionList()
    {
        GridPosition unitPos = unit.GetGridPosition();
        int floorAmount = LevelGrid.Instance.GetFloorAmount();
        int minFloor = Mathf.Clamp(unitPos.floor - maxRange, 0, floorAmount - 1);
        int maxFloor = Mathf.Clamp(unitPos.floor + maxRange, 0, floorAmount - 1);

        List<GridPosition> rangeList = new List<GridPosition>();
        for (int x = -maxRange; x <= maxRange; x++)
        {
            for (int z = -maxRange; z <= maxRange; z++)
            {
                float sizeOffset = (unit.GetSize() - 1) * 0.5f;
                float dx = x - sizeOffset, dz = z - sizeOffset;
                if (Mathf.Sqrt(dx * dx + dz * dz) > maxRange) continue;

                for (int floor = minFloor; floor <= maxFloor; floor++)
                {
                    GridPosition testPos = new GridPosition(unitPos.x + x, unitPos.z + z, floor);
                    if (LevelGrid.Instance.IsValidGridPosition(testPos)
                        && PathFinding.Instance.IsWalkableGridPosition(testPos))
                        rangeList.Add(testPos);
                }
            }
        }
        return rangeList;
    }

    private bool IsTargetVisible(Unit target)
    {
        Vector3 startPos = shootPointTransform != null
            ? shootPointTransform.position
            : unit.GetWorldPosition() + Vector3.up * (unit.GetCollider().bounds.size.y * 0.8f);

        Vector3 targetPos = target.GetWorldPosition()
                          + Vector3.up * (target.GetCollider().bounds.size.y * 0.8f);
        Vector3 dir = (targetPos - startPos).normalized;
        float dist = Vector3.Distance(startPos, targetPos);

        return !Physics.Raycast(startPos, dir, dist, obstacleLayerMask);
    }

    // ─── AI ────────────────────────────────────────────────────────────

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        if (!LevelGrid.Instance.IsGridPositionOccupied(gridPosition)) return null;
        Unit target = LevelGrid.Instance.GetUnitListAtGridPosition(gridPosition)[0];
        if (!TeamHelper.IsHostile(unit.GetTeamType(), target.GetTeamType())) return null;

        // 이미 둔화 걸린 적은 덜 우선
        StatusEffectSystem ses = target.GetComponent<StatusEffectSystem>();
        int slowPenalty = (ses != null && ses.HasEffect(StatusEffectType.MovementReduce)) ? -10 : 0;

        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 60 + slowPenalty };
    }
}
