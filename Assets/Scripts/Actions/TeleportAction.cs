using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TeleportAction : BaseAction
{
    [Header("Teleport Settings")]
    [SerializeField] private int teleportRange = 6;
    [SerializeField] private float teleportDelay = 0.15f; // 이펙트 재생 시간 여유

    public event EventHandler<GridPosition> OnTeleportStarted;
    public event EventHandler<GridPosition> OnTeleportCompleted;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }

    public override string GetActionName() => "Teleport";
    public override string GetDescription() =>
        $"반경 {teleportRange}칸 내 원하는 위치로 즉시 이동한다.";


    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        ActionStart(onActionComplete);
        StartCoroutine(TeleportRoutine(gridPosition));
    }

    private IEnumerator TeleportRoutine(GridPosition targetGridPosition)
    {
        OnTeleportStarted?.Invoke(this, unit.GetGridPosition());

        yield return new WaitForSeconds(teleportDelay);

        float cellSize = LevelGrid.Instance.GetCellSize();
        float centerOffset = (unit.GetSize() - 1) * cellSize * 0.5f;
        Vector3 offsetVector = new Vector3(centerOffset, 0f, centerOffset);

        Vector3 targetWorldPos = LevelGrid.Instance.GetWorldPosition(targetGridPosition) + offsetVector;

        LayerMask snapMask = unit.GetGroundSnapLayerMask();
        if (snapMask != 0 &&
            Physics.Raycast(targetWorldPos + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 3f, snapMask))
        {
            targetWorldPos.y = groundHit.point.y;
        }

        transform.position = targetWorldPos;
        unit.SetGridPosition(targetGridPosition);

        OnTeleportCompleted?.Invoke(this, targetGridPosition);

        ActionComplete();
    }

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        GridPosition unitGridPosition = unit.GetGridPosition();
        int unitSize = unit.GetSize();
        int floorAmount = LevelGrid.Instance.GetFloorAmount();
        int minFloor = Mathf.Max(0, unitGridPosition.floor - teleportRange);
        int maxFloor = Mathf.Min(floorAmount - 1, unitGridPosition.floor + teleportRange);

        List<GridPosition> validList = new List<GridPosition>();

        for (int x = -teleportRange; x <= teleportRange; x++)
        {
            for (int z = -teleportRange; z <= teleportRange; z++)
            {
                // 원형 범위
                float sizeOffset = (unitSize - 1) * 0.5f;
                float distX = x - sizeOffset;
                float distZ = z - sizeOffset;
                if (Mathf.Sqrt(distX * distX + distZ * distZ) > teleportRange) continue;

                for (int floor = minFloor; floor <= maxFloor; floor++)
                {
                    GridPosition testPos = new GridPosition(
                        unitGridPosition.x + x,
                        unitGridPosition.z + z,
                        floor
                    );

                    if (!LevelGrid.Instance.IsValidGridPosition(testPos)) continue;
                    // [문제 해결] IsWalkableGridPosition → IsDirectlyTargetable로 교체
                    // 계단 exclusive 타일은 IsWalkable=false지만 착지 후 계단으로 나갈 수 있으므로
                    // 텔레포트 목적지로는 유효하다. (PathFinding.IsDirectlyTargetable 주석 참고)
                    if (!PathFinding.Instance.IsDirectlyTargetable(testPos)) continue;

                    // 자기 자신 위치 제외
                    if (testPos == unitGridPosition) continue;

                    // 다른 유닛이 점유한 타일 제외
                    if (unitSize > 1)
                    {
                        bool occupied = false;
                        for (int i = 0; i < unitSize && !occupied; i++)
                            for (int j = 0; j < unitSize && !occupied; j++)
                            {
                                GridPosition subPos = testPos + new GridPosition(i, j, 0);
                                if (IsOccupiedByOtherUnit(subPos)) occupied = true;
                            }
                        if (!occupied) validList.Add(testPos);
                    }
                    else
                    {
                        if (!IsOccupiedByOtherUnit(testPos))
                            validList.Add(testPos);
                    }
                }
            }
        }

        return validList;
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

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        // AI는 순간이동으로 적에게 더 가까워지는 위치를 선호
        ShootAction shootAction = unit.GetAction<ShootAction>();
        int targetCount = shootAction != null ? shootAction.GetTargetCountAtPosition(gridPosition) : 0;

        if (targetCount > 0)
            return new EnemyAIAction { gridPosition = gridPosition, actionValue = 60 + targetCount * 10 };

        MeleeAction meleeAction = unit.GetAction<MeleeAction>();
        if (meleeAction != null)
        {
            // 가장 가까운 플레이어와의 거리가 줄어드는 위치를 선호
            float minDist = float.MaxValue;
            foreach (Unit playerUnit in UnitManager.Instance.GetFriendlyUnitList())
            {
                if (playerUnit.IsStealthed()) continue;
                float dist = Vector3.Distance(
                    LevelGrid.Instance.GetWorldPosition(gridPosition),
                    playerUnit.GetWorldPosition()
                );
                if (dist < minDist) minDist = dist;
            }
            int value = Mathf.RoundToInt(40f - minDist);
            return new EnemyAIAction { gridPosition = gridPosition, actionValue = Mathf.Clamp(value, 0, 39) };
        }

        return null;
    }
}
