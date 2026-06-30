using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 땅의 아무 칸이나 클릭해 그 방향으로 화살을 발사, 일직선상에 있는 모든 적에게 피해를 입히는 궁수 스킬.
/// 적이 있는 칸을 직접 조준할 필요 없이 사거리 내 어떤 방향이든 자유롭게 겨냥할 수 있다.
/// 모션·애니메이션 이벤트 흐름은 BowAction과 동일하게 맞춰 같은 활쏘기 모션을 재사용한다.
/// </summary>
public class PiercingArrowAction : BaseAction
{
    protected override string DefaultActionName() => "관통 화살";
    public override ActionCategory GetActionCategory() => ActionCategory.Attack;

    private enum State
    {
        Aiming,
        Shooting,
        Cooloff
    }

    public class OnShootEventArgs : EventArgs
    {
        public Unit shootingUnit;
    }

    [Header("사거리·피해")]
    [SerializeField] private int maxShootDistance = 8;
    [SerializeField] private int shootDamage = 8;
    [SerializeField] private float hitForce = 800f;
    [Tooltip("조준 방향과 적이 일직선으로 인정될 각도 허용 오차(도)")]
    [SerializeField] private float lineAngleTolerance = 8f;

    [Header("발사체")]
    [SerializeField] private Transform arrowProjectilePrefab;
    [SerializeField] private Transform shootPointTransform;
    [SerializeField] private Transform arrowInBow;
    [SerializeField] private LayerMask obstacleLayerMask;

    private State state;
    // 조준 지점 (땅 클릭 좌표). 유닛이 아니라 월드 좌표/방향 기준으로 조준한다.
    private Vector3 aimWorldPosition;
    private Vector3 aimDirection;
    // 화살이 실제로 도달하는 최종 지점(가장 먼 적 또는 사거리/장애물 끝)
    private Vector3 finalHitWorldPosition;
    // 조준 방향 일직선상에 있는, 가까운 순서로 정렬된 모든 적
    private List<Unit> lineTargets;
    private float rotateSpeed = 10f;
    private bool arrowShot = false;
    // BowAction과 같은 활쏘기 애니메이션·이벤트를 공유하므로,
    // 실제로 이 액션이 발사 대기 중일 때만 ShootArrow() 호출에 반응하도록 구분한다.
    private bool awaitingArrowRelease = false;

    // 유효 타일 목록 캐시
    private List<GridPosition> cachedValidGridPositionList;
    private bool isCacheDirty = true;

    // BowAction과 동일한 이벤트 이름을 재사용해 같은 활쏘기 애니메이션을 그대로 탄다.
    public event EventHandler<OnShootEventArgs> OnStartDrawing;
    public event EventHandler OnStopShooting;

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

    public override string GetDescription() =>
        $"화살을 발사해 조준선 일직선상의 모든 적에게 {shootDamage + unit.GetAttackPower()} 피해를 입힌다. (사거리 {maxShootDistance})";

    // 적이 있는 칸뿐 아니라 사거리 내 모든 땅 칸을 클릭 가능하게 한다 (AOEAction과 동일한 방식).
    public override List<GridPosition> GetValidActionGridPositionList()
    {
        if (!isCacheDirty && cachedValidGridPositionList != null) return cachedValidGridPositionList;

        cachedValidGridPositionList = new List<GridPosition>();
        GridPosition unitGridPosition = unit.GetGridPosition();

        int floorAmount = LevelGrid.Instance.GetFloorAmount();
        int minFloor = Mathf.Clamp(unitGridPosition.floor - maxShootDistance, 0, floorAmount - 1);
        int maxFloor = Mathf.Clamp(unitGridPosition.floor + maxShootDistance, 0, floorAmount - 1);

        for (int x = -maxShootDistance; x <= maxShootDistance; x++)
        {
            for (int z = -maxShootDistance; z <= maxShootDistance; z++)
            {
                float sizeOffset = (unit.GetSize() - 1) * 0.5f;
                float distanceX = x - sizeOffset;
                float distanceZ = z - sizeOffset;
                if (Mathf.Sqrt(distanceX * distanceX + distanceZ * distanceZ) > maxShootDistance) continue;

                for (int floor = minFloor; floor <= maxFloor; floor++)
                {
                    GridPosition testGridPosition = new GridPosition(unitGridPosition.x + x, unitGridPosition.z + z, floor);

                    if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;
                    if (!PathFinding.Instance.IsDirectlyTargetable(testGridPosition)) continue;

                    cachedValidGridPositionList.Add(testGridPosition);
                }
            }
        }
        isCacheDirty = false;
        return cachedValidGridPositionList;
    }

    private Vector3 GetShootOrigin()
    {
        return shootPointTransform != null
            ? shootPointTransform.position
            : unit.GetWorldPosition() + Vector3.up * (unit.GetCollider().bounds.size.y * 0.8f);
    }

    public override List<GridPosition> GetActionRangeGridPositionList()
    {
        // 클릭 가능한 범위와 동일하므로 그대로 재사용
        return GetValidActionGridPositionList();
    }

    /// <summary>
    /// 그리드 비주얼에서 빨간색으로 표시할 피해 범위.
    /// 시전자 → 마우스 오버 중인 타일 방향으로 일직선 전체(장애물에 막히는 지점까지)를 보여준다.
    /// </summary>
    public override List<GridPosition> GetDamageAffectedGridPosition(GridPosition targetGridPosition)
    {
        List<GridPosition> affected = new List<GridPosition>();
        if (!LevelGrid.Instance.IsValidGridPosition(targetGridPosition)) return affected;

        Vector3 origin = GetShootOrigin();
        Vector3 dir = LevelGrid.Instance.GetWorldPosition(targetGridPosition) - origin;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) dir = transform.forward;
        dir.Normalize();

        float cellSize = LevelGrid.Instance.GetCellSize();
        float maxWorldDistance = maxShootDistance * cellSize;

        // 장애물에 막히면 그 지점까지만 표시
        float travelLimit = maxWorldDistance;
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxWorldDistance, obstacleLayerMask))
            travelLimit = hit.distance;

        // [버그 수정] 샘플링 높이를 시전자 발 높이(현재 층)로 고정한다.
        //   origin(어깨/조준점 높이)을 그대로 쓰면 LevelGrid.GetGridPosition()이 월드 Y로
        //   층을 계산할 때(RoundToInt(y / FLOOR_HEIGHT)) 한 층 위로 잘못 판정되어,
        //   위층에 미리 배치된 그리드 비주얼 타일이 표시되는 버그가 있었다.
        //   (그 타일은 물리적으로 한 층 위에 있어 캐릭터 머리 높이쯤에 떠 보였다.)
        float groundY = unit.GetWorldPosition().y;

        // cellSize 절반 간격으로 촘촘히 샘플링해 직선이 지나가는 칸을 빠짐없이 수집
        GridPosition? lastPos = null;
        for (float d = 0f; d <= travelLimit; d += cellSize * 0.5f)
        {
            Vector3 worldPos = origin + dir * d;
            worldPos.y = groundY;
            GridPosition gridPos = LevelGrid.Instance.GetGridPosition(worldPos);
            if (!LevelGrid.Instance.IsValidGridPosition(gridPos)) continue;
            if (lastPos.HasValue && lastPos.Value == gridPos) continue;
            affected.Add(gridPos);
            lastPos = gridPos;
        }
        return affected;
    }

    public override void TakeAction(GridPosition gridPosition, Action onShootComplete)
    {
        Vector3 origin = GetShootOrigin();
        aimWorldPosition = LevelGrid.Instance.GetWorldPosition(gridPosition);

        aimDirection = aimWorldPosition - origin;
        aimDirection.y = 0f;
        if (aimDirection.sqrMagnitude < 0.0001f)
        {
            // 자기 발밑을 클릭한 경우 등 — 현재 바라보는 방향을 그대로 사용
            aimDirection = transform.forward;
        }
        aimDirection.Normalize();

        lineTargets = CollectLineTargets(origin, aimDirection, out finalHitWorldPosition);

        state = State.Aiming;
        ActionStart(onShootComplete);
        StartCoroutine(StateCheck());
    }

    /// <summary>
    /// origin에서 dir 방향 일직선상(각도 오차 이내)에 있는 모든 적을 가까운 순서로 정렬해 반환한다.
    /// 장애물에 가로막히면 그 지점부터는 포함하지 않는다.
    /// finalHitPos: 화살이 실제로 멈추는 지점 — 가장 먼 적 위치, 없으면 장애물 지점 또는 사거리 끝.
    /// </summary>
    private List<Unit> CollectLineTargets(Vector3 origin, Vector3 dir, out Vector3 finalHitPos)
    {
        float maxWorldDistance = maxShootDistance * LevelGrid.Instance.GetCellSize();

        // 화살 경로상 가장 먼저 막히는 장애물까지의 거리를 구해 사거리를 제한한다.
        float travelLimit = maxWorldDistance;
        if (Physics.Raycast(origin, dir, out RaycastHit obstacleHit, maxWorldDistance, obstacleLayerMask))
        {
            travelLimit = obstacleHit.distance;
        }

        var candidates = new List<(Unit unit, float dist)>();
        foreach (Unit candidate in UnitManager.Instance.GetUnitList())
        {
            if (!TeamHelper.IsHostile(unit.GetTeamType(), candidate.GetTeamType())) continue;
            if (candidate.IsStealthed()) continue;

            Vector3 candidatePos = candidate.GetWorldPosition() + Vector3.up * (candidate.GetCollider().bounds.size.y * 0.8f);
            Vector3 toCandidate = candidatePos - origin;
            Vector3 toCandidateFlat = toCandidate; toCandidateFlat.y = 0f;

            float dist = toCandidateFlat.magnitude;
            if (dist < 0.01f || dist > travelLimit + 0.01f) continue;

            float angle = Vector3.Angle(dir, toCandidateFlat.normalized);
            if (angle > lineAngleTolerance) continue;

            candidates.Add((candidate, dist));
        }
        candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

        var result = new List<Unit>();
        foreach (var (candidateUnit, dist) in candidates)
            result.Add(candidateUnit);

        // 적중 여부와 관계없이 장애물에 막히지 않는 한 항상 사거리 끝까지 날아간다.
        finalHitPos = origin + dir * travelLimit;

        return result;
    }

    private IEnumerator StateCheck()
    {
        while (state == State.Aiming)
        {
            AimToDirection();
            yield return null;
        }

        arrowShot = false;
        awaitingArrowRelease = true;
        OnStartDrawing?.Invoke(this, new OnShootEventArgs { shootingUnit = unit });
        if (arrowInBow != null) arrowInBow.gameObject.SetActive(true);

        // 애니메이션 이벤트(ShootArrow)가 호출될 때까지 대기 — BowAction과 동일한 흐름
        yield return new WaitUntil(() => arrowShot);
        if (arrowInBow != null) arrowInBow.gameObject.SetActive(false);

        yield return new WaitForSeconds(0.3f);
        state = State.Cooloff;
        OnStopShooting?.Invoke(this, EventArgs.Empty);
        ActionComplete();
    }

    private void AimToDirection()
    {
        Vector3 moveDir = aimDirection;
        transform.forward = Vector3.Slerp(transform.forward, moveDir, Time.deltaTime * rotateSpeed);

        if (Vector3.Angle(transform.forward, moveDir) < 1f)
        {
            state = State.Shooting;
        }
    }

    /// <summary>애니메이션 이벤트(AnimationEventRelay.ShootArrow)에서 호출.</summary>
    public void ShootArrow()
    {
        // 다른 액션(BowAction 등)이 같은 애니메이션 이벤트를 공유해서 호출한 경우 무시
        if (!awaitingArrowRelease) return;
        awaitingArrowRelease = false;

        if (arrowProjectilePrefab != null)
        {
            Vector3 spawnOrigin = GetShootOrigin();

            Transform arrowTransform = Instantiate(arrowProjectilePrefab, spawnOrigin, Quaternion.identity);
            ArrowProjectile arrowProjectile = arrowTransform.GetComponent<ArrowProjectile>();
            arrowProjectile.OnArrowHit += ArrowProjectile_OnArrowHit;
            arrowProjectile.Setup(finalHitWorldPosition);
        }
        else
        {
            // 발사체 프리팹이 없으면 즉시 피해 적용
            ApplyDamageToAllLineTargets();
        }

        arrowShot = true;
    }

    private void ArrowProjectile_OnArrowHit(object sender, ArrowProjectile.OnBulletHitEventArgs e)
    {
        ApplyDamageToAllLineTargets();
    }

    private void ApplyDamageToAllLineTargets()
    {
        if (lineTargets == null) return;

        foreach (Unit target in lineTargets)
        {
            if (target == null) continue;

            Vector3 hitDir = (target.GetWorldPosition() - unit.GetWorldPosition()).normalized;
            target.GetHitReaction().SetHitDirection(hitDir);
            target.GetHitReaction().SetHitForce(hitForce);
            target.Damage(unit.CalculateDamage(shootDamage));
        }
    }

    // [성능 수정] AI 평가에서 Physics.Raycast를 제거.
    //
    // [원인]
    //   GetBestEnemyAIAction()은 GetValidActionGridPositionList()가 반환하는 모든 칸(사거리 30이면
    //   원형으로 약 2800칸)마다 GetEnemyAIAction()을 호출한다. 기존 코드는 그 안에서 매번
    //   CollectLineTargets → Physics.Raycast(장애물 체크)를 실행했으므로, 적 턴 한 번에
    //   라이캐스트가 수천 번 발생해 심각한 렉이 발생했다. (MoveAction의 A* 프리즈와 같은 패턴.)
    //
    // [해결]
    //   AI가 "이 방향이 대략 얼마나 좋은가"를 가늠하는 데는 장애물까지 정확히 계산할 필요가 없다.
    //   라이캐스트 없이 각도·거리만으로 적중 예상 수를 세는 가벼운 카운트로 대체한다.
    //   실제 발사 시(TakeAction)에는 여전히 정확한 CollectLineTargets(장애물 포함)를 사용한다.
    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        Vector3 origin = GetShootOrigin();
        Vector3 dir = LevelGrid.Instance.GetWorldPosition(gridPosition) - origin;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return null;
        dir.Normalize();

        int hitCount = CountLineTargetsFast(origin, dir);
        if (hitCount == 0) return null;

        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 900 + hitCount * 150 };
    }

    /// <summary>
    /// AI 평가 전용 — 라이캐스트 없이 각도·거리만으로 일직선상에 걸리는 적 수를 센다.
    /// 장애물을 고려하지 않으므로 약간 과대평가될 수 있지만, 우선순위 비교용으로는 충분하다.
    /// </summary>
    private int CountLineTargetsFast(Vector3 origin, Vector3 dir)
    {
        float maxWorldDistance = maxShootDistance * LevelGrid.Instance.GetCellSize();
        int count = 0;

        foreach (Unit candidate in UnitManager.Instance.GetUnitList())
        {
            if (!TeamHelper.IsHostile(unit.GetTeamType(), candidate.GetTeamType())) continue;
            if (candidate.IsStealthed()) continue;

            Vector3 toCandidate = candidate.GetWorldPosition() - origin;
            toCandidate.y = 0f;

            float dist = toCandidate.magnitude;
            if (dist < 0.01f || dist > maxWorldDistance + 0.01f) continue;

            if (Vector3.Angle(dir, toCandidate.normalized) > lineAngleTolerance) continue;

            count++;
        }
        return count;
    }

    public int GetMaxShootDistance() => maxShootDistance;
}
