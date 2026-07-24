using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지정한 위치에 즉시 빛이 터지며 범위 내 적에게는 피해를, 아군에게는 보호막을 부여하는 단발성 스킬.
/// 투사체가 날아가지 않고 그 자리에서 바로 터진다.
/// LightOrbAction과 같은 시전 모션·애니메이션 트리거(isIceOrb)만 공유한다.
/// </summary>
public class HolyBurstAction : BaseAction
{
    protected override string DefaultActionName() => "신성 폭발";
    public override ActionCategory GetActionCategory() => ActionCategory.Attack;
    private enum State { Aiming, Shooting, Cooloff }

    public class OnShootEventArgs : EventArgs
    {
        public Unit shootingUnit;
    }

    [Header("Range")]
    [SerializeField] private int maxRange = 7;
    [SerializeField] private int burstRadius = 1; // 효과 반경 (칸)
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private Transform shootPointTransform;

    [Header("적 피해")]
    [SerializeField] private int damage = 18;

    [Header("아군 보호막")]
    [SerializeField] private int barrierAmount = 30;
    [SerializeField] private int barrierDuration = 2;

    [Header("Impact VFX")]
    [Tooltip("타겟 지점에서 즉시 터지는 이펙트")]
    [SerializeField] private GameObject impactVFXPrefab;
    [SerializeField] private Vector3 impactVFXOffset = Vector3.zero;
    [SerializeField] private float impactVFXLifetime = 3f;

    [Header("Cast VFX")]
    [Tooltip("이 액션을 선택해서 범위를 지정하는 동안 표시되는 이펙트. 영속 오브젝트(유닛)에 부모로 붙이지 않는다.")]
    [SerializeField] private Transform castVFXPrefab;
    [SerializeField] private Transform castVFXSpawnPoint;
    [SerializeField] private float castVFXHideDelay = 1.5f;

    private State state;
    private GridPosition pendingTargetPosition;
    private Vector3 aimWorldPosition;
    private float rotateSpeed = 10f;
    private bool orbShot = false;
    private GameObject activeCastVFX;

    private List<GridPosition> cachedValidList;
    private bool isCacheDirty = true;

    /// <summary>LightOrbAction과 같은 트리거(isIceOrb)를 재생하기 위해 동일한 이벤트 형태를 사용한다.</summary>
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

    private void OnCacheInvalidated(object sender, EventArgs e) => isCacheDirty = true;

    public override string GetDescription()
    {
        int atk = unit.GetAttackPower();
        return $"지정한 위치 반경 {burstRadius}칸에 빛을 터뜨려, 적에게는 {damage + atk} 피해를, " +
               $"아군에게는 보호막 {barrierAmount}을 부여한다. ({barrierDuration}턴)";
    }

    // ─── 시전 이펙트 (범위 지정 중 표시) ─────────────────────────────────

    private void OnSelectedActionChanged(object sender, BaseAction selectedAction)
    {
        if (selectedAction == this)
            ShowCastVFX();
        else
            HideCastVFX();
    }

    /// <summary>
    /// [버그 수정] 유닛(영속 오브젝트)을 부모로 Instantiate하면 "Cannot instantiate objects with
    /// a parent which is persistent" 경고가 뜬다. 부모 지정 없이 스폰하고 직접 참조로 관리한다.
    /// </summary>
    private void ShowCastVFX()
    {
        if (castVFXPrefab == null || activeCastVFX != null) return;
        Transform spawnPoint = castVFXSpawnPoint != null ? castVFXSpawnPoint : (shootPointTransform != null ? shootPointTransform : unit.transform);
        activeCastVFX = Instantiate(castVFXPrefab, spawnPoint.position, spawnPoint.rotation).gameObject;
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

    // ─── 범위 타일 ────────────────────────────────────────────────────

    private List<GridPosition> GetBurstArea(GridPosition center)
    {
        List<GridPosition> list = new List<GridPosition>();
        for (int x = -burstRadius; x <= burstRadius; x++)
        {
            for (int z = -burstRadius; z <= burstRadius; z++)
            {
                GridPosition pos = new GridPosition(center.x + x, center.z + z, center.floor);
                if (LevelGrid.Instance.IsValidGridPosition(pos))
                    list.Add(pos);
            }
        }
        return list;
    }

    // ─── 유효 액션 위치 — 적/아군 점유 여부와 무관하게 사거리 내 모든 칸 ────

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
                if (Mathf.Sqrt(x * x + z * z) > maxRange) continue;

                for (int floor = minFloor; floor <= maxFloor; floor++)
                {
                    GridPosition testPos = new GridPosition(unitPos.x + x, unitPos.z + z, floor);
                    if (!LevelGrid.Instance.IsValidGridPosition(testPos)) continue;
                    if (!PathFinding.Instance.IsDirectlyTargetable(testPos)) continue;

                    if (obstacleLayerMask != 0)
                    {
                        Vector3 startPos = GetShootOrigin();
                        Vector3 targetPos = LevelGrid.Instance.GetWorldPosition(testPos) + Vector3.up * 0.5f;
                        if (Physics.Raycast(startPos, (targetPos - startPos).normalized, Vector3.Distance(startPos, targetPos), obstacleLayerMask))
                            continue;
                    }

                    cachedValidList.Add(testPos);
                }
            }
        }

        isCacheDirty = false;
        return cachedValidList;
    }

    public override List<GridPosition> GetActionRangeGridPositionList()
    {
        return GetValidActionGridPositionList();
    }

    public override List<GridPosition> GetDamageAffectedGridPosition(GridPosition targetGridPosition)
    {
        return GetBurstArea(targetGridPosition);
    }

    private Vector3 GetShootOrigin()
    {
        return shootPointTransform != null
            ? shootPointTransform.position
            : unit.GetWorldPosition() + Vector3.up * (unit.GetCollider().bounds.size.y * 0.8f);
    }

    // ─── 액션 실행 ────────────────────────────────────────────────────

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        pendingTargetPosition = gridPosition;
        aimWorldPosition = LevelGrid.Instance.GetWorldPosition(gridPosition);
        orbShot = false;
        state = State.Aiming;
        HideCastVFXDelayed();
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
        OnStartShooting?.Invoke(this, new OnShootEventArgs { shootingUnit = unit });

        // 애니메이션 이벤트(FireIceOrb → AnimationEventRelay.ShootIceOrb)가 ShootOrb()를 호출할 때까지 대기.
        // 이벤트가 없을 경우를 대비해 2초 안전 타임아웃을 둔다.
        float timeout = 2f;
        while (!orbShot && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        if (!orbShot) ShootOrb();

        yield return new WaitForSeconds(0.3f);
        state = State.Cooloff;
        ActionComplete();
    }

    private void AimToTarget()
    {
        Vector3 targetPos = aimWorldPosition;
        targetPos.y = transform.position.y;
        Vector3 moveDir = (targetPos - transform.position).normalized;

        if (moveDir == Vector3.zero) { state = State.Shooting; return; }

        transform.forward = Vector3.Slerp(transform.forward, moveDir, Time.deltaTime * rotateSpeed);

        if (Vector3.Angle(transform.forward, moveDir) < 1f)
            state = State.Shooting;
    }

    /// <summary>애니메이션 이벤트에서 AnimationEventRelay.ShootIceOrb()를 통해 호출됨. 그 자리에서 즉시 터진다.</summary>
    public void ShootOrb()
    {
        // [버그 수정] isIceOrb 트리거를 재사용하는 스킬(WindBlast 등)이 FireHolyBurst() 이벤트를 오발하는 문제 방지
        if (state != State.Shooting) return;
        if (orbShot) return;
        orbShot = true;

        ApplyBurst(pendingTargetPosition);
    }

    private void ApplyBurst(GridPosition center)
    {
        if (impactVFXPrefab != null)
        {
            Vector3 vfxPos = LevelGrid.Instance.GetWorldPosition(center) + impactVFXOffset;
            GameObject vfx = Instantiate(impactVFXPrefab, vfxPos, Quaternion.identity);
            Destroy(vfx, impactVFXLifetime);
        }

        HashSet<Unit> hitUnits = new HashSet<Unit>();
        foreach (GridPosition pos in GetBurstArea(center))
        {
            if (!LevelGrid.Instance.IsGridPositionOccupied(pos)) continue;

            foreach (Unit target in LevelGrid.Instance.GetUnitListAtGridPosition(pos))
            {
                if (hitUnits.Contains(target)) continue; // 사이즈 2 이상 유닛 중복 방지
                hitUnits.Add(target);

                if (TeamHelper.IsHostile(unit.GetTeamType(), target.GetTeamType()))
                {
                    target.Damage(unit.CalculateDamage(damage));
                }
                else
                {
                    BarrierSystem bs = target.GetComponent<BarrierSystem>();
                    bs?.ApplyBarrier(barrierAmount, barrierDuration);
                }
            }
        }
    }

    // ─── AI ──────────────────────────────────────────────────────────

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        int enemyHitCount = 0;
        int allyHitCount = 0;
        foreach (GridPosition pos in GetBurstArea(gridPosition))
        {
            if (!LevelGrid.Instance.IsGridPositionOccupied(pos)) continue;
            foreach (Unit target in LevelGrid.Instance.GetUnitListAtGridPosition(pos))
            {
                if (TeamHelper.IsHostile(unit.GetTeamType(), target.GetTeamType()))
                    enemyHitCount++;
                else
                    allyHitCount++;
            }
        }
        if (enemyHitCount == 0 && allyHitCount == 0) return null;
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 40 + enemyHitCount * 40 + allyHitCount * 25 };
    }
}
