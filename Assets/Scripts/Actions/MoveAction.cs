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
    private int currentPositionIndex;
    private GridPosition targetGridPosition;

    // ĳ�� �� ����ȭ��
    private ShootAction _cachedShootAction;
    private List<GridPosition> cachedValidGridPositionList;
    private bool isCacheDirty = true;

    public event EventHandler OnStartMoving;
    public event EventHandler OnStopMoving;
    public event EventHandler<OnChangeFloorStartedEventArgs> OnChangeFloorsStarted;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 0; // �̵� �׼� �Ҹ� ����
        leftMoveDistance = maxMoveDistance;
    }

    private void Start()
    {
        // ���� �ٲ� ������ �̵� �Ÿ� �ʱ�ȭ
        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
    }

    private void TurnSystem_OnTurnChanged(object sender, EventArgs empty)
    {
        leftMoveDistance = maxMoveDistance;
        MarkCacheDirty();
    }

    public void MarkCacheDirty()
    {
        isCacheDirty = true;
        cachedValidGridPositionList = null;
    }

    public override string GetActionName() => "Move";

    public override void TakeAction(GridPosition gridPosition, Action onMovingComplete)
    {
        targetGridPosition = gridPosition;

        // ��� ã�� �� �ܼ�ȭ
        List<GridPosition> path = PathFinding.Instance.FindPath(unit.GetGridPosition(), gridPosition, unit.GetSize(), out int pathLength);
        List<GridPosition> simplifiedPath = SimplifyPath(path);

        targetPosList = new List<Vector3>();
        float cellSize = LevelGrid.Instance.GetCellSize();

        // ���� ũ�Ⱑ 1���� ũ�� �߽����� ���߱� ���� ������ ���
        float centerOffset = (unit.GetSize() - 1) * cellSize * 0.5f;
        Vector3 offsetVector = new Vector3(centerOffset, 0f, centerOffset);

        foreach (GridPosition pos in simplifiedPath)
        {
            targetPosList.Add(LevelGrid.Instance.GetWorldPosition(pos) + offsetVector);
        }

        // �̹� ���� ��ġ�� �ִٸ� ����Ʈ���� ����
        if (targetPosList.Count > 0 && Vector3.Distance(transform.position, targetPosList[0]) < 0.1f)
        {
            targetPosList.RemoveAt(0);
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

            // ��(Floor)�� �ٸ��� ����/���� ���� Ʈ����
            if (targetPosGrid.floor != currentPosGrid.floor)
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
                // �Ϲ� ���� �̵�
                while (Vector3.Distance(transform.position, targetPos) > stoppingDistance)
                {
                    Vector3 moveDir = (targetPos - transform.position).normalized;
                    if (moveDir != Vector3.zero)
                    {
                        // ȸ�� ó��
                        Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotateSpeed);

                        // �չ���� �̵������� ��� ���� ��ġ�� ���� ���� (�ڿ������� ȸ�� �̵�)
                        if (Vector3.Dot(transform.forward, moveDir) > 0.7f)
                        {
                            transform.position += moveDir * moveSpeed * Time.deltaTime;
                        }
                    }
                    yield return null;
                }
            }

            transform.position = targetPos;
            currentPositionIndex++;
        }

        OnStopMoving?.Invoke(this, EventArgs.Empty);
        unit.SetGridPosition(targetGridPosition);
        ActionComplete();
    }

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        if (!isCacheDirty && cachedValidGridPositionList != null) return cachedValidGridPositionList;

        List<GridPosition> validGridPositionList = new List<GridPosition>();
        GridPosition unitGridPosition = unit.GetGridPosition();
        int unitSize = unit.GetSize();
        int range = Mathf.RoundToInt(leftMoveDistance);

        for (int x = -range; x <= range; x++)
        {
            for (int z = -range; z <= range; z++)
            {
                // ������ ��(-1, 0, 1) �˻�
                for (int floor = unitGridPosition.floor - 1; floor <= unitGridPosition.floor + 1; floor++)
                {
                    if (floor < 0 || floor >= LevelGrid.Instance.GetFloorAmount()) continue;

                    GridPosition testGridPosition = new GridPosition(unitGridPosition.x + x, unitGridPosition.z + z, floor);

                    if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;
                    if (unitGridPosition == testGridPosition) continue;

                    bool isValidPath = true;
                    // ���� ����(Size > 1)�� ��� ���� ���� ��� üũ
                    if (unitSize > 1)
                    {
                        for (int i = 0; i < unitSize; i++)
                        {
                            for (int j = 0; j < unitSize; j++)
                            {
                                GridPosition subPos = testGridPosition + new GridPosition(i, j, 0);
                                if (!LevelGrid.Instance.IsValidGridPosition(subPos) ||
                                    !PathFinding.Instance.IsWalkableGridPosition(subPos) ||
                                    IsOccupiedByOtherUnit(subPos))
                                {
                                    isValidPath = false;
                                    break;
                                }
                            }
                            if (!isValidPath) break;
                        }
                    }
                    else
                    {
                        if (!PathFinding.Instance.IsWalkableGridPosition(testGridPosition) || IsOccupiedByOtherUnit(testGridPosition))
                        {
                            isValidPath = false;
                        }
                    }

                    if (isValidPath)
                    {
                        int pathLength = PathFinding.Instance.GetPathLength(unitGridPosition, testGridPosition, unitSize);
                        // ���� ��� �Ÿ��� ���� �̵� �Ÿ� �̳����� Ȯ��
                        if (pathLength != 0 && pathLength <= leftMoveDistance * 10f)
                        {
                            validGridPositionList.Add(testGridPosition);
                        }
                    }
                }
            }
        }

        cachedValidGridPositionList = validGridPositionList;
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

            // ������ �ٲ�� ����(���̴� ��)�� ����Ʈ�� �߰�
            if (prev.x - curr.x != curr.x - next.x ||
                prev.z - curr.z != curr.z - next.z ||
                prev.floor - curr.floor != curr.floor - next.floor)
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