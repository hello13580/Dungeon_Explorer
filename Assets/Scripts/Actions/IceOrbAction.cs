using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    [SerializeField] private float slowValue = 0.3f;
    [SerializeField] private int slowDuration = 2;

    [Header("Projectile")]
    [SerializeField] private Transform iceOrbPrefab;

    [Header("Cast VFX")]
    [SerializeField] private Transform castVFXPrefab;
    [SerializeField] private float castVFXHideDelay = 1.5f;

    private State state;
    private Unit targetUnit;
    private float rotateSpeed = 10f;
    private bool orbShot = false;
    private GameObject activeCastVFX;

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

        // 이 액션이 선택/해제될 때 시전 VFX 표시/제거
        UnitActionSystem.Instance.OnSelectedActionChanged += OnSelectedActionChanged;
    }

    private void OnDestroy()
    {
        TurnSystem.Instance.OnTurnChanged -= OnCacheInvalidated;
        BaseAction.OnAnyActionEnded -= OnCacheInvalidated;

        if (UnitActionSystem.Instance != null)
            UnitActionSystem.Instance.OnSelectedActionChanged -= OnSelectedActionChanged;

        HideCastVFX();
    }

    private void OnSelectedActionChanged(object sender, BaseAction selectedAction)
    {
        if (selectedAction == this)
            ShowCastVFX();
        else
            HideCastVFX();
    }

    private void ShowCastVFX()
    {
        if (castVFXPrefab == null || activeCastVFX != null) return;

        Transform spawnPoint = shootPointTransform != null ? shootPointTransform : unit.transform;
        activeCastVFX = Instantiate(castVFXPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint).gameObject;
    }

    private void HideCastVFX()
    {
        if (activeCastVFX == null) return;
        Destroy(activeCastVFX);
        activeCastVFX = null;
    }

    private void HideCastVFXDelayed()
    {
        if (activeCastVFX == null) return;
        StartCoroutine(HideCastVFXRoutine());
    }

    private IEnumerator HideCastVFXRoutine()
    {
        yield return new WaitForSeconds(castVFXHideDelay);
        HideCastVFX();
    }

    private void OnCacheInvalidated(object sender, EventArgs e) => isCacheDirty = true;

    public override string GetActionName() => "Ice Orb";
    public override string GetDescription() =>
        $"사거리 {maxRange} 내 적에게 {damage + unit.GetAttackPower()} 피해를 입히고 이동속도를 {Mathf.RoundToInt(slowValue * 100)}% 감소시킨다. ({slowDuration}턴)";


    // ─── 액션 실행 ─────────────────────────────────────────────────────

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        List<Unit> targetList = LevelGrid.Instance.GetUnitListAtGridPosition(gridPosition);
        if (targetList.Count == 0) return;

        HideCastVFXDelayed();

        targetUnit = targetList[0];
        orbShot = false;
        state = State.Aiming;
        ActionStart(onActionComplete);
        StartCoroutine(StateCheck());
    }

    private IEnumerator StateCheck()
    {
        while (state == State.Aiming)
        {
            AimToTarget();
            yield return null;
        }

        orbShot = false;
        OnStartShooting?.Invoke(this, new OnShootEventArgs
        {
            targetUnit = targetUnit,
            shootingUnit = unit
        });

        // 애니메이션 이벤트(FireIceOrb)가 ShootOrb()를 호출할 때까지 대기
        yield return new WaitUntil(() => orbShot);

        yield return new WaitForSeconds(0.3f);
        state = State.Cooloff;
        ActionComplete();
    }

    private void AimToTarget()
    {
        Vector3 targetPos = targetUnit.transform.position;
        targetPos.y = transform.position.y;
        Vector3 moveDir = (targetPos - transform.position).normalized;

        transform.forward = Vector3.Slerp(transform.forward, moveDir, Time.deltaTime * rotateSpeed);

        if (Vector3.Angle(transform.forward, moveDir) < 1f)
            state = State.Shooting;
    }

    /// <summary>애니메이션 이벤트에서 UnitAnimator.FireIceOrb()를 통해 호출됨.</summary>
    public void ShootOrb()
    {
        if (targetUnit == null) return;

        Vector3 spawnPos = shootPointTransform != null
            ? shootPointTransform.position
            : unit.GetWorldPosition() + Vector3.up * (unit.GetCollider().bounds.size.y * 0.8f);

        Vector3 targetPos = targetUnit.GetWorldPosition()
                          + Vector3.up * (targetUnit.GetCollider().bounds.size.y * 0.8f);

        Transform orb = Instantiate(iceOrbPrefab, spawnPos, Quaternion.identity);
        IceOrbProjectile projectile = orb.GetComponent<IceOrbProjectile>();

        if (projectile == null)
        {
            orbShot = true;
            return;
        }

        projectile.OnOrbHit += IceOrbProjectile_OnOrbHit;
        projectile.Setup(targetPos);

        orbShot = true;
    }

    private void IceOrbProjectile_OnOrbHit(object sender, IceOrbProjectile.OnOrbHitEventArgs e)
    {
        if (targetUnit == null) return;

        Vector3 hitDir = (e.hitPosition - unit.GetWorldPosition()).normalized;
        targetUnit.GetHitReaction().SetHitDirection(hitDir);
        targetUnit.GetHitReaction().SetHitForce(400f);
        targetUnit.Damage(unit.CalculateDamage(damage));

        StatusEffectSystem ses = targetUnit.GetComponent<StatusEffectSystem>();
        if (ses != null)
        {
            ses.AddEffect(new StatusEffect(
                StatusEffectType.MovementReduce,
                slowValue,
                slowDuration,
                "둔화",
                StackingMode.ExtendDuration
            ));
        }
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
                        && PathFinding.Instance.IsDirectlyTargetable(testPos))
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

        StatusEffectSystem ses = target.GetComponent<StatusEffectSystem>();
        int slowPenalty = (ses != null && ses.HasEffect(StatusEffectType.MovementReduce)) ? -10 : 0;
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 60 + slowPenalty };
    }
}
