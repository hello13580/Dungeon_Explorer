using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대상 적에게 달려가서 근접 공격하는 액션.
/// 대상 유닛의 인접 칸 중 경로가 있는 칸으로 이동한 뒤 피해를 입힌다.
/// </summary>
public class DashAttackAction : BaseAction
{
    protected override string DefaultActionName() => "돌진 공격";
    [Header("Range")]
    [SerializeField] private int dashRange = 6;       // 대상을 선택할 수 있는 최대 거리

    [Header("Damage")]
    [SerializeField] private int damage = 15;         // 기본 피해 (+ 시전자 공격력)
    [SerializeField] private float hitForce = 600f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 12f;   // 돌진 이동 속도

    public event EventHandler OnDashMoveStarted;   // 돌진 이동 시작 → 달리기 애니메이션
    public event EventHandler OnDashMoveEnded;     // 돌진 이동 완료 → 달리기 애니메이션 종료
    public event EventHandler OnDashAttackStarted; // 공격 시점 → 공격 애니메이션
    public event EventHandler OnDashAttackEnded;   // 액션 전체 종료

    private Unit targetUnit;

    // 유효 타일 캐시
    private List<GridPosition> cachedValidList;
    private bool isCacheDirty = true;

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

    private void OnCacheInvalidated(object sender, EventArgs e) => isCacheDirty = true;    public override string GetDescription() =>
        $"사거리 {dashRange} 내 적에게 돌진해 {damage + unit.GetAttackPower()} 피해를 입힌다.";


    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        targetUnit = LevelGrid.Instance.GetUnitListAtGridPosition(gridPosition)[0];
        ActionStart(onActionComplete);
        StartCoroutine(DashAttackRoutine());
    }

    private IEnumerator DashAttackRoutine()
    {
        if (targetUnit == null)
        {
            ActionComplete();
            yield break;
        }

        // 대상 인접 칸 중 도달 가능한 칸 탐색
        GridPosition landPos = FindLandingPosition(targetUnit.GetGridPosition());

        // ── 이동 ──────────────────────────────────────────────────────
        if (landPos != unit.GetGridPosition())
        {
            List<GridPosition> path = PathFinding.Instance.FindPath(
                unit.GetGridPosition(), landPos, unit.GetSize(), out int _);

            if (path != null)
            {
                // 달리기 애니메이션 시작
                OnDashMoveStarted?.Invoke(this, EventArgs.Empty);

                float cellSize = LevelGrid.Instance.GetCellSize();
                float centerOffset = (unit.GetSize() - 1) * cellSize * 0.5f;
                Vector3 offsetVec = new Vector3(centerOffset, 0f, centerOffset);

                foreach (GridPosition pathPos in path)
                {
                    Vector3 targetPos = LevelGrid.Instance.GetWorldPosition(pathPos) + offsetVec;

                    LayerMask snapMask = unit.GetGroundSnapLayerMask();
                    if (snapMask != 0 &&
                        Physics.Raycast(targetPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 3f, snapMask))
                    {
                        targetPos.y = hit.point.y;
                    }

                    while (Vector3.Distance(transform.position, targetPos) > 0.05f)
                    {
                        Vector3 dir = (targetPos - transform.position);
                        dir.y = 0f;
                        dir.Normalize();

                        if (dir != Vector3.zero)
                            transform.forward = Vector3.Lerp(transform.forward, dir, Time.deltaTime * 20f);

                        transform.position += dir * moveSpeed * Time.deltaTime;

                        Vector3 pos = transform.position;
                        pos.y = Mathf.MoveTowards(pos.y, targetPos.y, moveSpeed * Time.deltaTime);
                        transform.position = pos;

                        yield return null;
                    }
                }

                // 그리드 위치 확정 후 달리기 애니메이션 종료
                LevelGrid.Instance.RemoveUnitAtGridPosition(unit.GetGridPosition(), unit);
                unit.SetGridPosition(landPos);
                LevelGrid.Instance.AddUnitAtGridPosition(landPos, unit);

                OnDashMoveEnded?.Invoke(this, EventArgs.Empty);
            }
        }

        // ── 공격 ──────────────────────────────────────────────────────
        // 대상 방향으로 회전
        Vector3 lookDir = (targetUnit.GetWorldPosition() - unit.GetWorldPosition());
        lookDir.y = 0f;
        if (lookDir != Vector3.zero)
            transform.forward = lookDir.normalized;

        // 공격 애니메이션 트리거
        OnDashAttackStarted?.Invoke(this, EventArgs.Empty);

        yield return new WaitForSeconds(0.15f);

        if (targetUnit != null)
        {
            Vector3 targetWorldPos = targetUnit.GetWorldPosition();
            float targetHeight = targetUnit.GetCollider().bounds.max.y;
            targetWorldPos.y = targetHeight * 0.8f;

            Vector3 hitDir = (targetWorldPos - unit.GetWorldPosition()).normalized;
            targetUnit.GetHitReaction().SetHitDirection(hitDir);
            targetUnit.GetHitReaction().SetHitForce(hitForce);
            targetUnit.Damage(unit.CalculateDamage(damage));
        }

        yield return new WaitForSeconds(0.4f);

        OnDashAttackEnded?.Invoke(this, EventArgs.Empty);
        ActionComplete();
    }

    /// <summary>
    /// 대상 인접 칸 중 PathFinding으로 도달 가능한 칸을 반환한다.
    /// 도달 가능한 칸이 없으면 현재 위치를 반환 (이동 없이 공격).
    /// </summary>
    private GridPosition FindLandingPosition(GridPosition targetPos)
    {
        int unitSize = unit.GetSize();

        // 대상 유닛 주변 인접 칸 후보
        List<GridPosition> candidates = new List<GridPosition>();
        for (int x = -unitSize; x <= unitSize; x++)
        {
            for (int z = -unitSize; z <= unitSize; z++)
            {
                // 대각선 포함 인접 칸 (대상 자신 제외)
                if (x == 0 && z == 0) continue;
                if (Mathf.Abs(x) > 1 && Mathf.Abs(z) > 1) continue;

                GridPosition candidate = targetPos + new GridPosition(x, z, 0);
                if (!LevelGrid.Instance.IsValidGridPosition(candidate)) continue;
                if (!PathFinding.Instance.IsDirectlyTargetable(candidate)) continue;
                if (LevelGrid.Instance.IsGridPositionOccupied(candidate)) continue;

                candidates.Add(candidate);
            }
        }

        // 거리 가장 가까운 순서로 정렬 후 경로가 있는 첫 번째 칸 선택
        GridPosition unitPos = unit.GetGridPosition();
        candidates.Sort((a, b) =>
        {
            int da = Mathf.Abs(a.x - unitPos.x) + Mathf.Abs(a.z - unitPos.z);
            int db = Mathf.Abs(b.x - unitPos.x) + Mathf.Abs(b.z - unitPos.z);
            return da.CompareTo(db);
        });

        foreach (GridPosition candidate in candidates)
        {
            List<GridPosition> path = PathFinding.Instance.FindPath(unitPos, candidate, unitSize, out int _);
            if (path != null) return candidate;
        }

        // 도달 가능한 인접 칸 없음 → 현재 위치에서 그냥 공격
        return unitPos;
    }

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        if (!isCacheDirty && cachedValidList != null) return cachedValidList;

        cachedValidList = new List<GridPosition>();
        GridPosition unitGridPos = unit.GetGridPosition();

        // dashRange 내 적 유닛 위치를 유효 타일로 등록
        foreach (Unit enemy in UnitManager.Instance.GetEnemyUnitList())
        {
            if (enemy.IsStealthed()) continue;
            if (!TeamHelper.IsHostile(unit.GetTeamType(), enemy.GetTeamType())) continue;

            GridPosition enemyPos = enemy.GetGridPosition();
            int dist = Mathf.Abs(enemyPos.x - unitGridPos.x) + Mathf.Abs(enemyPos.z - unitGridPos.z);
            if (dist > dashRange) continue;

            // 해당 적 인접에 도달 가능한 칸이 하나라도 있어야 유효
            GridPosition landing = FindLandingPosition(enemyPos);
            if (landing != unitGridPos || enemyPos == unitGridPos)
                cachedValidList.Add(enemyPos);
        }

        isCacheDirty = false;
        return cachedValidList;
    }

    public override List<GridPosition> GetActionRangeGridPositionList()
    {
        // MoveAction처럼 원형 범위로 시각화
        List<GridPosition> rangeList = new List<GridPosition>();
        GridPosition unitGridPos = unit.GetGridPosition();
        int unitSize = unit.GetSize();

        for (int x = -dashRange; x <= dashRange; x++)
        {
            for (int z = -dashRange; z <= dashRange; z++)
            {
                // 크기가 1보다 큰 유닛은 중심점을 기준으로 원형 판정
                float sizeOffset = (unitSize - 1) * 0.5f;
                float distX = x - sizeOffset;
                float distZ = z - sizeOffset;
                if (Mathf.Sqrt(distX * distX + distZ * distZ) > dashRange) continue;

                GridPosition testPos = unitGridPos + new GridPosition(x, z, 0);
                if (LevelGrid.Instance.IsValidGridPosition(testPos))
                    rangeList.Add(testPos);
            }
        }
        return rangeList;
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 180 };
    }
}
