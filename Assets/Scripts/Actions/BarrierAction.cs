using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BarrierAction : BaseAction
{
    [SerializeField] private int maxBarrierDistance = 5;
    [SerializeField] private int BarrierAmount = 40;
    [SerializeField] private int BarrierDuration = 3;
    [SerializeField] private LayerMask obstacleLayerMask;

    private Unit targetUnit;
    private float rotateSpeed = 10f;

    private List<GridPosition> cachedValidGridPositionList;
    private bool isCacheDirty = true;

    public static event EventHandler<OnBarrierEventArgs> OnAnyBarrier;
    public event EventHandler<OnBarrierEventArgs> OnBarrier;

    public class OnBarrierEventArgs : EventArgs
    {
        public Unit targetUnit;
        public Unit BarrierUsingUnit;
    }

    protected override void Awake()
    {
        base.Awake();
        actionCost = 2;
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

    public override string GetActionName() => "Barrier";

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        if (!isCacheDirty && cachedValidGridPositionList != null) return cachedValidGridPositionList;

        cachedValidGridPositionList = new List<GridPosition>();
        GridPosition unitGridPosition = unit.GetGridPosition();

        int floorAmount = LevelGrid.Instance.GetFloorAmount();
        int minFloor = Mathf.Clamp(unitGridPosition.floor - maxBarrierDistance, 0, floorAmount - 1);
        int maxFloor = Mathf.Clamp(unitGridPosition.floor + maxBarrierDistance, 0, floorAmount - 1);

        for (int x = -maxBarrierDistance; x <= maxBarrierDistance; x++)
        {
            for (int z = -maxBarrierDistance; z <= maxBarrierDistance; z++)
            {
                float sizeOffset = (unit.GetSize() - 1) * 0.5f;
                float distX = x - sizeOffset;
                float distZ = z - sizeOffset;
                if (Mathf.Sqrt(distX * distX + distZ * distZ) > maxBarrierDistance) continue;

                for (int floor = minFloor; floor <= maxFloor; floor++)
                {
                    GridPosition testGridPosition = new GridPosition(unitGridPosition.x + x, unitGridPosition.z + z, floor);
                    if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;
                    if (!LevelGrid.Instance.IsGridPositionOccupied(testGridPosition)) continue;

                    foreach (Unit candidate in LevelGrid.Instance.GetUnitListAtGridPosition(testGridPosition))
                    {
                        if (TeamHelper.IsHostile(unit.GetTeamType(), candidate.GetTeamType())) continue;
                        if (!IsTargetVisible(candidate)) continue;

                        cachedValidGridPositionList.Add(testGridPosition);
                    }
                }
            }
        }

        isCacheDirty = false;
        return cachedValidGridPositionList;
    }

    public override List<GridPosition> GetActionRangeGridPositionList()
    {
        List<GridPosition> rangeList = new List<GridPosition>();
        GridPosition unitGridPosition = unit.GetGridPosition();

        int floorAmount = LevelGrid.Instance.GetFloorAmount();
        int minFloor = Mathf.Clamp(unitGridPosition.floor - maxBarrierDistance, 0, floorAmount - 1);
        int maxFloor = Mathf.Clamp(unitGridPosition.floor + maxBarrierDistance, 0, floorAmount - 1);

        for (int x = -maxBarrierDistance; x <= maxBarrierDistance; x++)
        {
            for (int z = -maxBarrierDistance; z <= maxBarrierDistance; z++)
            {
                float sizeOffset = (unit.GetSize() - 1) * 0.5f;
                float distX = x - sizeOffset;
                float distZ = z - sizeOffset;
                if (Mathf.Sqrt(distX * distX + distZ * distZ) > maxBarrierDistance) continue;

                for (int floor = minFloor; floor <= maxFloor; floor++)
                {
                    GridPosition testGridPosition = new GridPosition(unitGridPosition.x + x, unitGridPosition.z + z, floor);
                    if (LevelGrid.Instance.IsValidGridPosition(testGridPosition)
                        && PathFinding.Instance.IsWalkableGridPosition(testGridPosition))
                    {
                        rangeList.Add(testGridPosition);
                    }
                }
            }
        }
        return rangeList;
    }

    private bool IsTargetVisible(Unit target)
    {
        Vector3 startPos = unit.GetWorldPosition() + Vector3.up * (unit.GetCollider().bounds.size.y * 0.8f);
        Vector3 targetPos = target.GetWorldPosition() + Vector3.up * (target.GetCollider().bounds.size.y * 0.8f);
        float distance = Vector3.Distance(startPos, targetPos);
        return !Physics.Raycast(startPos, (targetPos - startPos).normalized, distance, obstacleLayerMask);
    }

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        List<Unit> targetUnitList = LevelGrid.Instance.GetUnitListAtGridPosition(gridPosition);
        if (targetUnitList.Count == 0)
        {
            Debug.LogError("������ Ÿ�� ����: " + gridPosition.ToString());
            return;
        }

        targetUnit = targetUnitList[0];
        ActionStart(onActionComplete);
        StartCoroutine(BarrierRoutine());
    }

    private IEnumerator BarrierRoutine()
    {
        // Ÿ�� �������� ȸ��
        Vector3 targetPos = targetUnit.transform.position;
        targetPos.y = transform.position.y;
        Vector3 dir = (targetPos - transform.position).normalized;

        while (Vector3.Angle(transform.forward, dir) > 1f)
        {
            transform.forward = Vector3.Slerp(transform.forward, dir, Time.deltaTime * rotateSpeed);
            yield return null;
        }

        yield return new WaitForSeconds(0.3f);

        BarrierSystem barrierSystem = targetUnit.GetComponent<BarrierSystem>();
        if (barrierSystem != null)
            barrierSystem.ApplyBarrier(BarrierAmount, BarrierDuration);

        OnBarrier?.Invoke(this, new OnBarrierEventArgs { targetUnit = targetUnit, BarrierUsingUnit = unit });
        OnAnyBarrier?.Invoke(this, new OnBarrierEventArgs { targetUnit = targetUnit, BarrierUsingUnit = unit });

        yield return new WaitForSeconds(0.5f);

        ActionComplete();
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        List<Unit> unitsAtPos = LevelGrid.Instance.GetUnitListAtGridPosition(gridPosition);
        if (unitsAtPos.Count == 0) return null;

        Unit candidate = unitsAtPos[0];
        // �Ʊ��̰� ü���� �������� ���� �켱����
        int missingHealthPercent = 100 - Mathf.RoundToInt(candidate.GetHealthNormalized() * 100f);
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 200 + missingHealthPercent * 3 };
    }

    public int GetMaxHealDistance() => maxBarrierDistance;
    public int GetHealAmount() => BarrierAmount;
}
