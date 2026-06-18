using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MoveAction : BaseAction
{
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
    [SerializeField] private float approachRange = 3f;
    [SerializeField] private float visionRange = 10f;

    private float leftMoveDistance;
    private List<Vector3> targetPosList;
    private List<bool> isLinkStep;
    private int currentPositionIndex;
    private GridPosition targetGridPosition;

    // ĳ�� �� ����ȭ��
    private ShootAction _cachedShootAction;
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

    public override string GetActionName() => "Move";

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
                // 일반 이동 및 경사로 이동
                while (Vector3.Distance(transform.position, targetPos) > stoppingDistance)
                {
                    Vector3 moveDir = (targetPos - transform.position);
                    // Y축 변화량을 0으로 만들어 앞뒤로 기울어지는 것(Pitch/Roll)을 방지합니다.
                    moveDir.y = 0f;
                    moveDir.Normalize();

                    if (moveDir != Vector3.zero)
                    {
                        // 이제 오직 좌우(Y축 회전)로만 회전하게 됩니다.
                        Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotateSpeed);

                        // 이동 자체는 높낮이(targetPos)가 반영되어야 하므로 원래 벡터의 방향을 따로 씁니다.
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

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        if (leftMoveDistance <= 0f) return null;

        if (_cachedShootAction == null) _cachedShootAction = unit.GetAction<ShootAction>();

        // 1. ���� ������ ��ġ�ΰ�?
        int targetCount = _cachedShootAction != null ? _cachedShootAction.GetTargetCountAtPosition(gridPosition) : 0;
        if (targetCount > 0)
        {
            int baseValue = 50 + (10 * targetCount);
            float distToTarget = Math.Abs(GetClosestTargetDistance(gridPosition) - approachRange);
            int distanceBonus = Mathf.RoundToInt(10f - distToTarget);

            return new EnemyAIAction { gridPosition = gridPosition, actionValue = Mathf.Clamp(baseValue + distanceBonus, 50, 100) };
        }

        // 2. ������ �Ұ��������� �÷��̾�� �����ؾ� �ϴ°�?
        // ?꾨컻 以묒씤 ?좊떅???덉쑝硫??대떦 ?좊떅?먭쾶 ?곗꽑 ?묎렐
        if (TauntManager.Instance != null && TauntManager.Instance.HasActiveTaunt())
        {
            Unit taunted = TauntManager.Instance.GetTauntedUnit();
            if (taunted != null)
            {
                int tauntPathLength = PathFinding.Instance.GetPathLength(gridPosition, taunted.GetGridPosition(), unit.GetSize());
                if (tauntPathLength > 0 && tauntPathLength <= visionRange * 10f)
                {
                    int tauntProximity = 40 - (tauntPathLength / 10);
                    return new EnemyAIAction { gridPosition = gridPosition, actionValue = Mathf.Clamp(tauntProximity + 200, 200, 240) };
                }
            }
        }
        Unit closestPlayer = GetClosestPlayerByPath(gridPosition);
        if (closestPlayer != null)
        {
            int pathLength = PathFinding.Instance.GetPathLength(gridPosition, closestPlayer.GetGridPosition(), unit.GetSize());
            if (pathLength > visionRange * 10f) return null;

            int floorPenalty = (closestPlayer.GetGridPosition().floor > gridPosition.floor) ? -20 : 0;
            int proximityValue = 40 - (pathLength / 10);

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

    private Unit GetClosestPlayerByPath(GridPosition fromPos)
    {
        Unit closestUnit = null;
        int minPathLength = int.MaxValue;

        foreach (Unit playerUnit in UnitManager.Instance.GetFriendlyUnitList())
        {
            if (playerUnit.IsStealthed()) continue;
            int pathLength = PathFinding.Instance.GetPathLength(fromPos, playerUnit.GetGridPosition(), unit.GetSize());
            if (pathLength > 0 && pathLength < minPathLength)
            {
                minPathLength = pathLength;
                closestUnit = playerUnit;
            }
        }
        return closestUnit;
    }
}