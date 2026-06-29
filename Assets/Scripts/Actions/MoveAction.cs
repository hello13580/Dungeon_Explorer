using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveAction : BaseAction
{
    protected override string DefaultActionName() => "이동";
    public class OnChangeFloorStartedEventArgs : EventArgs
    {
        public GridPosition unitGridPosition;
        public GridPosition targetPosition;
    }

    [Header("Movement Settings")]
    [SerializeField] private float maxMoveDistance = 5f;
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float rotateSpeed = 20f;

    [Header("AI Settings")]
    [SerializeField] private float approachRangeMin = 1f;
    [SerializeField] private float approachRangeMax = 1f;
    [SerializeField] private float visionRange = 10f;

    private float leftMoveDistance;
    private List<Vector3> targetPosList;
    private List<bool> isLinkStep;
    private int currentPositionIndex;
    private GridPosition targetGridPosition;

    // 캐시 및 최적화
    private ShootAction _cachedShootAction;
    private BowAction _cachedBowAction;
    private List<GridPosition> cachedValidGridPositionList;
    private bool isCacheDirty = true;
    private GridPosition cachedFromPosition;

    public event EventHandler OnStartMoving;
    public event EventHandler OnStopMoving;
    public event EventHandler<OnChangeFloorStartedEventArgs> OnChangeFloorsStarted;

    // 이동 중 유닛이 새 그리드 타일에 도달할 때마다 발생 — 경유 타일 감지용
    public static event EventHandler<GridPosition> OnAnyUnitSteppedOnTile;


    protected override void Awake()
    {
        base.Awake();
        actionCost = 0; // �̵� �׼� �Ҹ� ����
        leftMoveDistance = maxMoveDistance;
    }

    private void Start()
    {
        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
        // 문 등 오브젝트의 이동 가능 여부가 바뀌면 캐시를 갱신해야 함
        MapObject.OnAnyWalkableChanged += MapObject_OnAnyWalkableChanged;
    }

    private void OnDestroy()
    {
        TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
        MapObject.OnAnyWalkableChanged -= MapObject_OnAnyWalkableChanged;
    }

    // 같은 턴 안에서 문이 열리거나 닫히면 이동 가능 위치 캐시가 이전 상태로 남아
    // 닫힌 문이 초록색으로 표시되는 문제를 방지하기 위해 캐시를 무효화
    private void MapObject_OnAnyWalkableChanged(object sender, EventArgs e)
    {
        MarkCacheDirty();
    }

    private void TurnSystem_OnTurnChanged(object sender, EventArgs empty)
    {
        if (unit == null) return;

        float multiplier = 1f;
        StatusEffectSystem statusEffectSystem = unit.GetComponent<StatusEffectSystem>();
        if (statusEffectSystem != null)
        {
            // 속박 상태이면 이동 거리 0 (이동 불가)
            if (statusEffectSystem.IsRooted())
                multiplier = 0f;
            else
                multiplier = statusEffectSystem.GetMovementMultiplier();
        }

        leftMoveDistance = maxMoveDistance * multiplier;
        MarkCacheDirty();
    }

    public void MarkCacheDirty()
    {
        isCacheDirty = true;
        cachedValidGridPositionList = null;
    }

    /// <summary>
    /// 이동 거리를 회복한다. 둔화 등 상태이상이 있으면 multiplier가 낮아져 회복량이 줄어든다.
    /// SprintAction에서 호출.
    /// </summary>
    public void RestoreMoveDistance()
    {
        float multiplier = 1f;
        StatusEffectSystem ses = unit.GetComponent<StatusEffectSystem>();
        if (ses != null)
        {
            // 속박 상태면 이동 거리 0 — 전력질주도 효과 없음
            if (ses.IsRooted())
                multiplier = 0f;
            else
                multiplier = ses.GetMovementMultiplier();
        }

        leftMoveDistance = maxMoveDistance * multiplier;
        MarkCacheDirty();
    }

    public float GetLeftMoveDistance() => leftMoveDistance;
    public float GetMaxMoveDistance() => maxMoveDistance;
    public override void TakeAction(GridPosition gridPosition, Action onMovingComplete)
    {
        targetGridPosition = gridPosition;

        List<GridPosition> path = PathFinding.Instance.FindPath(unit.GetGridPosition(), gridPosition, unit.GetSize(), out int pathLength);
        // FindPath는 경로가 없으면 null을 반환함. 예: 닫힌 문 위치로 이동 시도할 때
        // null을 SimplifyPath에 넘기면 NullReferenceException 발생하므로 여기서 처리
        if (path == null)
        {
            onMovingComplete?.Invoke();
            return;
        }
        List<GridPosition> simplifiedPath = SimplifyPath(path);

        targetPosList = new List<Vector3>();
        isLinkStep = new List<bool>();
        float cellSize = LevelGrid.Instance.GetCellSize();

        // ���� ũ�Ⱑ 1���� ũ�� �߽����� ���߱� ���� ������ ���
        float centerOffset = (unit.GetSize() - 1) * cellSize * 0.5f;
        Vector3 offsetVector = new Vector3(centerOffset, 0f, centerOffset);

        for (int i = 0; i < simplifiedPath.Count; i++)
        {
            GridPosition pos = simplifiedPath[i];

            bool isLink = i > 0 && PathFinding.Instance.IsAnyLink(simplifiedPath[i - 1], pos);

            Vector3 targetPos = LevelGrid.Instance.GetWorldPosition(pos) + offsetVector;
            LayerMask snapMask = unit.GetGroundSnapLayerMask();
            if (snapMask != 0 &&
                Physics.Raycast(targetPos + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 3f, snapMask))
            {
                targetPos.y = groundHit.point.y;
            }
            targetPosList.Add(targetPos);
            isLinkStep.Add(isLink);
        }

        // �̹� ���� ��ġ�� �ִٸ� ����Ʈ���� ����
        if (targetPosList.Count > 0 && Vector3.Distance(transform.position, targetPosList[0]) < 0.1f)
        {
            targetPosList.RemoveAt(0);
            isLinkStep.RemoveAt(0);
        }

        currentPositionIndex = 0;
        float moveCost = pathLength / 10f;

        if (leftMoveDistance >= moveCost)
        {
            leftMoveDistance -= moveCost;
            MarkCacheDirty();

            ActionStart(onMovingComplete);
            OnStartMoving?.Invoke(this, EventArgs.Empty);
            StartCoroutine(MoveRoutine());
        }
        else
        {
            onMovingComplete?.Invoke();
        }
    }

    private IEnumerator MoveRoutine()
    {
        while (currentPositionIndex < targetPosList.Count)
        {
            Vector3 targetPos = targetPosList[currentPositionIndex];
            float stoppingDistance = 0.05f;

            GridPosition targetPosGrid = LevelGrid.Instance.GetGridPosition(targetPos);
            GridPosition currentPosGrid = LevelGrid.Instance.GetGridPosition(transform.position);
            bool currentIsLink = isLinkStep[currentPositionIndex];

            // 링크 기반 이동(사다리 등)일 때만 층 전환 이벤트 발동
            // 계단 인접 타일 이동은 링크가 아니므로 층이 바뀌어도 부드럽게 이동
            if (currentIsLink && targetPosGrid.floor != currentPosGrid.floor)
            {
                OnChangeFloorsStarted?.Invoke(this, new OnChangeFloorStartedEventArgs
                {
                    unitGridPosition = currentPosGrid,
                    targetPosition = targetPosGrid
                });
                yield return new WaitForSeconds(0.3f); // �ִϸ��̼� ��� �ð�
            }
            else
            {
                // [버그 수정] 도달 판정을 XZ 거리로만 한다.
                //
                // [원인]
                //   targetPos.y는 지형 스냅(Raycast)으로 결정되므로 유닛의 실제 y와 미세하게 다를 수 있다.
                //   사이즈 2 유닛은 centerOffset이 더해지므로 이 오차가 더 자주 발생한다.
                //   이전 코드(Vector3.Distance)는 y 오차가 0.05f를 초과하면 루프에 진입했고,
                //   그 안에서 moveDir.y = 0 → normalize → zero 벡터 → if(moveDir != zero) 실패
                //   → 이동도 회전도 없이 yield return null만 반복 → 무한 루프 → 턴이 안 끝나는 버그.
                //
                // [해결]
                //   XZ 성분만으로 거리를 계산해 도달 여부를 판정한다.
                //   루프 탈출 후 transform.position = targetPos 로 y까지 정확히 스냅된다.
                while (new Vector2(transform.position.x - targetPos.x, transform.position.z - targetPos.z).magnitude > stoppingDistance)
                {
                    Vector3 moveDir = (targetPos - transform.position);
                    moveDir.y = 0f;
                    moveDir.Normalize();

                    if (moveDir != Vector3.zero)
                    {
                        // XZ 기준으로만 회전(Y축만 변경) → Pitch/Roll 방지
                        Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotateSpeed);

                        // 실제 이동 방향은 y를 포함한 원본 벡터를 사용해 경사로 등반을 반영한다.
                        // Dot > 0.7f: 유닛이 목표 방향에 충분히 정렬된 뒤에만 전진 (뒤로 미끄러짐 방지)
                        Vector3 actualMoveDir = (targetPos - transform.position).normalized;
                        if (Vector3.Dot(transform.forward, moveDir) > 0.7f)
                        {
                            transform.position += actualMoveDir * moveSpeed * Time.deltaTime;
                        }
                    }
                    yield return null;
                }
            }

            transform.position = targetPos;
            // 경유 타일 도달 알림 — 장판 등 지형 효과가 경유 여부를 기록할 수 있도록
            OnAnyUnitSteppedOnTile?.Invoke(this, LevelGrid.Instance.GetGridPosition(targetPos));
            currentPositionIndex++;
        }

        OnStopMoving?.Invoke(this, EventArgs.Empty);
        unit.SetGridPosition(targetGridPosition);
        ActionComplete();
    }

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        GridPosition unitGridPosition = unit.GetGridPosition();
        if (!isCacheDirty && cachedValidGridPositionList != null && cachedFromPosition == unitGridPosition)
            return cachedValidGridPositionList;
        int unitSize = unit.GetSize();
        // PathFinding 비용 단위로 변환 (직선 1칸 = 10, 대각선 1칸 = 14)
        int maxCost = Mathf.RoundToInt(leftMoveDistance * 10f);

        // [최적화 전] 범위 내 타일마다 A* 전체 실행 → 범위가 넓을수록 타일 수가 제곱으로 증가
        // int range = Mathf.RoundToInt(leftMoveDistance);
        // for (int x = -range; x <= range; x++)
        // {
        //     for (int z = -range; z <= range; z++)
        //     {
        //         for (int floor = unitGridPosition.floor - 1; floor <= unitGridPosition.floor + 1; floor++)
        //         {
        //             ...walkable 체크...
        //             int pathLength = PathFinding.Instance.GetPathLength(unitGridPosition, testGridPosition, unitSize);
        //             // GetPathLength 내부에서 FindPath(A* 전체)를 실행 → 타일 수만큼 반복
        //             if (pathLength != 0 && pathLength <= leftMoveDistance * 10f)
        //                 validGridPositionList.Add(testGridPosition);
        //         }
        //     }
        // }

        // [최적화 후] 플러드 필(Dijkstra)로 한 번만 탐색 → 도달 가능한 타일만 자연스럽게 수집
        // GetReachableGridPositions 내부에서 비용이 maxCost를 초과하는 방향은 즉시 탐색 중단
        List<GridPosition> reachable = PathFinding.Instance.GetReachableGridPositions(unitGridPosition, maxCost, unitSize);

        List<GridPosition> validGridPositionList = new List<GridPosition>();
        foreach (GridPosition pos in reachable)
        {
            // 플러드 필은 walkable 여부만 보므로, 다른 유닛이 서있는 위치는 여기서 후처리로 제외
            // 크기 2 이상 유닛은 점유하는 모든 서브 타일을 확인해야 함
            if (unitSize > 1)
            {
                bool occupied = false;
                for (int i = 0; i < unitSize && !occupied; i++)
                    for (int j = 0; j < unitSize && !occupied; j++)
                        if (IsOccupiedByOtherUnit(pos + new GridPosition(i, j, 0)))
                            occupied = true;
                if (!occupied) validGridPositionList.Add(pos);
            }
            else
            {
                if (!IsOccupiedByOtherUnit(pos))
                    validGridPositionList.Add(pos);
            }
        }

        cachedValidGridPositionList = validGridPositionList;
        cachedFromPosition = unitGridPosition;
        isCacheDirty = false;
        return validGridPositionList;
    }

    public List<GridPosition> GetMovementPreviewGridPositionList(GridPosition targetGridPosition)
    {
        List<GridPosition> path = PathFinding.Instance.FindPath(unit.GetGridPosition(), targetGridPosition, unit.GetSize(), out int pathLength);
        return path ?? new List<GridPosition>();
    }

    private bool IsOccupiedByOtherUnit(GridPosition gridPosition)
    {
        if (LevelGrid.Instance.IsGridPositionOccupied(gridPosition))
        {
            Unit occupant = LevelGrid.Instance.GetUnitListAtGridPosition(gridPosition)[0];
            return occupant != unit;
        }
        return false;
    }

    private List<GridPosition> SimplifyPath(List<GridPosition> path)
    {
        if (path.Count < 3) return path;

        List<GridPosition> simplifiedPath = new List<GridPosition> { path[0] };
        for (int i = 1; i < path.Count - 1; i++)
        {
            GridPosition prev = path[i - 1];
            GridPosition curr = path[i];
            GridPosition next = path[i + 1];

            // 층이 바뀌는 구간의 타일은 항상 유지 (계단/경사로가 평지처럼 이동되는 것 방지)
            if (prev.floor != curr.floor || curr.floor != next.floor)
            {
                simplifiedPath.Add(curr);
                continue;
            }

            // 방향이 바뀌는 지점도 유지
            if (prev.x - curr.x != curr.x - next.x ||
                prev.z - curr.z != curr.z - next.z)
            {
                simplifiedPath.Add(curr);
            }
        }
        simplifiedPath.Add(path[path.Count - 1]);
        return simplifiedPath;
    }

    // --- AI ���� ---

    // [버그 수정] AI 평가 거리 계산을 직선 거리로 교체.
    //
    // [원인]
    //   이전 코드는 GetBestEnemyAIAction() → GetEnemyAIAction(pos) 에서
    //   GetPathLength(A*)를 호출했다. 이 함수는 이동 가능한 모든 위치(N개)마다 실행되므로
    //   총 N번의 A* 탐색이 단일 프레임에서 동기적으로 실행됐다.
    //   사이즈 2 유닛은 IsWalkableArea가 2×2 영역을 체크해 A*당 비용이 ~4배이고,
    //   2번째 턴부터 죽은 유닛의 그리드가 비워져 N이 급격히 증가하면서 심각한 프리즈가 발생했다.
    //
    // [해결]
    //   AI가 이동 위치를 평가하는 용도로는 직선 거리로 충분하다.
    //   실제 이동 경로는 TakeAction 시점에 A*로 정확히 계산하므로 이동 자체의 품질은 유지된다.
    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        if (leftMoveDistance <= 0f) return null;

        float cellSize = LevelGrid.Instance.GetCellSize();
        Vector3 fromWorld = LevelGrid.Instance.GetWorldPosition(gridPosition);

        // 도발 중이면 도발 대상 우선
        if (TauntManager.Instance != null && TauntManager.Instance.HasActiveTaunt())
        {
            Unit taunted = TauntManager.Instance.GetTauntedUnit();
            if (taunted != null)
            {
                float td = Vector3.Distance(fromWorld, taunted.GetWorldPosition()) / cellSize;
                if (td <= visionRange)
                {
                    float distToRange = td < approachRangeMin ? approachRangeMin - td
                                      : td > approachRangeMax ? td - approachRangeMax
                                      : 0f;
                    int tauntValue = Mathf.RoundToInt(40f - distToRange * 2f);
                    return new EnemyAIAction { gridPosition = gridPosition, actionValue = Mathf.Clamp(tauntValue + 200, 200, 240) };
                }
            }
        }

        Unit closestPlayer = GetClosestPlayerByPath(gridPosition);
        if (closestPlayer != null)
        {
            float worldDist = Vector3.Distance(fromWorld, closestPlayer.GetWorldPosition());
            float distInCells = worldDist / cellSize;

            if (distInCells > visionRange) return null;

            int floorPenalty = (closestPlayer.GetGridPosition().floor != gridPosition.floor) ? -10 : 0;
            float distToRange = distInCells < approachRangeMin ? approachRangeMin - distInCells
                              : distInCells > approachRangeMax ? distInCells - approachRangeMax
                              : 0f;
            int proximityValue = Mathf.RoundToInt(40f - distToRange * 2f);
            return new EnemyAIAction { gridPosition = gridPosition, actionValue = Mathf.Clamp(proximityValue + floorPenalty, 0, 49) };
        }

        return null;
    }

    private float GetClosestTargetDistance(GridPosition fromPos)
    {
        float minDistance = float.MaxValue;
        Vector3 worldPos = LevelGrid.Instance.GetWorldPosition(fromPos);

        foreach (Unit playerUnit in UnitManager.Instance.GetFriendlyUnitList())
        {
            if (playerUnit.IsStealthed()) continue;
            float dist = Vector3.Distance(worldPos, playerUnit.GetWorldPosition());
            if (dist < minDistance) minDistance = dist;
        }

        return (minDistance == float.MaxValue) ? 0f : minDistance / LevelGrid.Instance.GetCellSize();
    }

    // AI 평가용: 직선 거리로 가장 가까운 플레이어를 찾는다.
    // 이전에는 GetPathLength(A*)를 사용했으나, 이동 가능한 모든 위치마다 호출되어
    // 사이즈 2 유닛(오우거 등)에서 N×M번의 A*가 한 프레임에 실행되며 프리즈가 발생했다.
    // AI 평가 목적에는 직선 거리로 충분하다.
    private Unit GetClosestPlayerByPath(GridPosition fromPos)
    {
        Unit closestUnit = null;
        float minDistance = float.MaxValue;
        Vector3 fromWorld = LevelGrid.Instance.GetWorldPosition(fromPos);

        foreach (Unit playerUnit in UnitManager.Instance.GetFriendlyUnitList())
        {
            if (playerUnit.IsStealthed()) continue;
            float dist = Vector3.Distance(fromWorld, playerUnit.GetWorldPosition());
            if (dist < minDistance)
            {
                minDistance = dist;
                closestUnit = playerUnit;
            }
        }
        return closestUnit;
    }
}