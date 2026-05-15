using System;
using System.Collections.Generic;
using UnityEngine;

public class AOEAction : BaseAction
{
    [SerializeField] private Transform grenadeProjectilePrefab;
    [SerializeField] private Transform shootPointTransform;
    [SerializeField] private LayerMask obstacleLayerMask;

    [SerializeField] private int maxRange = 7;
    [SerializeField] private int damageRadius = 1;
    [SerializeField] private float targetingYAxis = 0.5f;

    protected override void Awake()
    {
        base.Awake();
        // Awake에서 변수 초기화 (인스펙터 설정이 우선됨)
        actionCost = 1;
    }

    public override string GetActionName() => "Grenade";

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        ActionStart(onActionComplete);

        // 수류탄 생성 및 설정
        Transform grenadeTransform = Instantiate(grenadeProjectilePrefab, shootPointTransform.position, Quaternion.identity);
        GrenadeProjectile grenadeProjectile = grenadeTransform.GetComponent<GrenadeProjectile>();

        // 수류탄 투척 후 완료 시 ActionComplete 호출되도록 전달
        grenadeProjectile.Setup(gridPosition, damageRadius, this, ActionComplete);
    }

    public override List<GridPosition> GetActionRangeGridPositionList()
    {
        List<GridPosition> rangeList = new List<GridPosition>();
        GridPosition unitGridPosition = unit.GetGridPosition();

        int floorAmount = LevelGrid.Instance.GetFloorAmount();
        int minFloor = Mathf.Clamp(unitGridPosition.floor - maxRange, 0, floorAmount - 1);
        int maxFloor = Mathf.Clamp(unitGridPosition.floor + maxRange, 0, floorAmount - 1);

        for (int x = -maxRange; x <= maxRange; x++)
        {
            for (int z = -maxRange; z <= maxRange; z++)
            {
                // 원형 범위 체크
                if (Mathf.Sqrt(x * x + z * z) > maxRange) continue;

                for (int floor = minFloor; floor <= maxFloor; floor++)
                {
                    GridPosition testGridPosition = new GridPosition(unitGridPosition.x + x, unitGridPosition.z + z, floor);

                    if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;
                    if (!PathFinding.Instance.IsWalkableGridPosition(testGridPosition)) continue;

                    rangeList.Add(testGridPosition);
                }
            }
        }
        return rangeList;
    }

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        GridPosition unitGridPosition = unit.GetGridPosition();
        return GetValidActionGridPositionList(unitGridPosition);
    }

    public List<GridPosition> GetValidActionGridPositionList(GridPosition unitGridPosition)
    {
        List<GridPosition> validList = new List<GridPosition>();

        int floorAmount = LevelGrid.Instance.GetFloorAmount();
        int minFloor = Mathf.Clamp(unitGridPosition.floor - maxRange, 0, floorAmount - 1);
        int maxFloor = Mathf.Clamp(unitGridPosition.floor + maxRange, 0, floorAmount - 1);

        for (int x = -maxRange; x <= maxRange; x++)
        {
            for (int z = -maxRange; z <= maxRange; z++)
            {
                if (Mathf.Sqrt(x * x + z * z) > maxRange) continue;

                for (int floor = minFloor; floor <= maxFloor; floor++)
                {
                    GridPosition testGridPosition = new GridPosition(unitGridPosition.x + x, unitGridPosition.z + z, floor);

                    if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;
                    if (!PathFinding.Instance.IsWalkableGridPosition(testGridPosition)) continue;

                    // 투척 가능 여부 (장애물 체크)
                    Vector3 startPos;
                    if (shootPointTransform != null)
                    {
                        startPos = shootPointTransform.position;
                    }
                    else
                    {
                        startPos = unit.GetWorldPosition() + Vector3.up * (unit.GetCollider().bounds.size.y * 0.75f);
                    }

                    Vector3 targetPos = LevelGrid.Instance.GetWorldPosition(testGridPosition) + Vector3.up * targetingYAxis;
                    Vector3 dirToTarget = (targetPos - startPos).normalized;
                    float distance = Vector3.Distance(startPos, targetPos);

                    // 레이캐스트로 투척 경로에 벽이 있는지 확인
                    if (!Physics.Raycast(startPos, dirToTarget, distance, obstacleLayerMask))
                    {
                        validList.Add(testGridPosition);
                    }
                }
            }
        }
        return validList;
    }

    public override List<GridPosition> GetDamageAffectedGridPosition(GridPosition targetGridPosition)
    {
        return GetDamageAffectedGridPosition(targetGridPosition, damageRadius);
    }

    public List<GridPosition> GetDamageAffectedGridPosition(GridPosition targetGridPosition, int radius)
    {
        List<GridPosition> affectedList = new List<GridPosition>();

        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                GridPosition testGridPosition = targetGridPosition + new GridPosition(x, z, 0);

                if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;

                // 중심점은 무조건 포함, 그 외는 장애물 유무 확인
                if (targetGridPosition == testGridPosition || HasClearLineOfSight(targetGridPosition, testGridPosition))
                {
                    affectedList.Add(testGridPosition);
                }
            }
        }
        return affectedList;
    }

    private bool HasClearLineOfSight(GridPosition centerGridPosition, GridPosition targetGridPosition)
    {
        Vector3 centerPos = LevelGrid.Instance.GetWorldPosition(centerGridPosition) + Vector3.up * 1f;
        Vector3 targetPos = LevelGrid.Instance.GetWorldPosition(targetGridPosition) + Vector3.up * 1f;

        Vector3 dir = (targetPos - centerPos).normalized;
        float distance = Vector3.Distance(centerPos, targetPos);

        // 폭발 중심지에서 타겟 그리드 사이에 벽이 있는지 체크
        return !Physics.Raycast(centerPos, dir, distance, obstacleLayerMask);
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        // AI가 수류탄을 던질 때의 가치 판단 (현재 -40으로 되어 있으나 로직 추가 가능)
        return new EnemyAIAction
        {
            gridPosition = gridPosition,
            actionValue = -40
        };
    }
}