using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealAction : BaseAction
{
    [SerializeField] private int maxHealDistance = 5;
    [SerializeField] private int healAmount = 40;
    [SerializeField] private LayerMask obstacleLayerMask;

    private Unit targetUnit;
    private float rotateSpeed = 10f;

    private List<GridPosition> cachedValidGridPositionList;
    private bool isCacheDirty = true;

    public static event EventHandler<OnHealEventArgs> OnAnyHeal;
    public event EventHandler<OnHealEventArgs> OnHeal;

    public class OnHealEventArgs : EventArgs
    {
        public Unit targetUnit;
        public Unit healingUnit;
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

    public override string GetActionName() => "Heal";
    public override string GetDescription() =>
        $"사거리 {maxHealDistance} 내 아군의 체력을 {healAmount} 회복시킨다.";


    public override List<GridPosition> GetValidActionGridPositionList()
    {
        if (!isCacheDirty && cachedValidGridPositionList != null) return cachedValidGridPositionList;

        cachedValidGridPositionList = new List<GridPosition>();
        GridPosition unitGridPosition = unit.GetGridPosition();

        int floorAmount = LevelGrid.Instance.GetFloorAmount();
        int minFloor = Mathf.Clamp(unitGridPosition.floor - maxHealDistance, 0, floorAmount - 1);
        int maxFloor = Mathf.Clamp(unitGridPosition.floor + maxHealDistance, 0, floorAmount - 1);

        for (int x = -maxHealDistance; x <= maxHealDistance; x++)
        {
            for (int z = -maxHealDistance; z <= maxHealDistance; z++)
            {
                float sizeOffset = (unit.GetSize() - 1) * 0.5f;
                float distX = x - sizeOffset;
                float distZ = z - sizeOffset;
                if (Mathf.Sqrt(distX * distX + distZ * distZ) > maxHealDistance) continue;

                for (int floor = minFloor; floor <= maxFloor; floor++)
                {
                    GridPosition testGridPosition = new GridPosition(unitGridPosition.x + x, unitGridPosition.z + z, floor);
                    if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;
                    if (!LevelGrid.Instance.IsGridPositionOccupied(testGridPosition)) continue;

                    foreach (Unit candidate in LevelGrid.Instance.GetUnitListAtGridPosition(testGridPosition))
                    {
                        if (TeamHelper.IsHostile(unit.GetTeamType(), candidate.GetTeamType())) continue;
                        if (candidate.GetHealthNormalized() >= 1f) continue; // 이미 풀피면 제외
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
        int minFloor = Mathf.Clamp(unitGridPosition.floor - maxHealDistance, 0, floorAmount - 1);
        int maxFloor = Mathf.Clamp(unitGridPosition.floor + maxHealDistance, 0, floorAmount - 1);

        for (int x = -maxHealDistance; x <= maxHealDistance; x++)
        {
            for (int z = -maxHealDistance; z <= maxHealDistance; z++)
            {
                float sizeOffset = (unit.GetSize() - 1) * 0.5f;
                float distX = x - sizeOffset;
                float distZ = z - sizeOffset;
                if (Mathf.Sqrt(distX * distX + distZ * distZ) > maxHealDistance) continue;

                for (int floor = minFloor; floor <= maxFloor; floor++)
                {
                    GridPosition testGridPosition = new GridPosition(unitGridPosition.x + x, unitGridPosition.z + z, floor);
                    if (LevelGrid.Instance.IsValidGridPosition(testGridPosition)
                        && PathFinding.Instance.IsDirectlyTargetable(testGridPosition))
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
            Debug.LogError("회복하려는데 타겟 없음: " + gridPosition.ToString());
            return;
        }

        targetUnit = targetUnitList[0];
        ActionStart(onActionComplete);
        StartCoroutine(HealRoutine());
    }

    private IEnumerator HealRoutine()
    {
        // 타겟 방향으로 회전
        Vector3 targetPos = targetUnit.transform.position;
        targetPos.y = transform.position.y;
        Vector3 dir = (targetPos - transform.position).normalized;

        while (Vector3.Angle(transform.forward, dir) > 1f)
        {
            transform.forward = Vector3.Slerp(transform.forward, dir, Time.deltaTime * rotateSpeed);
            yield return null;
        }

        yield return new WaitForSeconds(0.3f);

        targetUnit.Heal(healAmount);
        OnHeal?.Invoke(this, new OnHealEventArgs { targetUnit = targetUnit, healingUnit = unit });
        OnAnyHeal?.Invoke(this, new OnHealEventArgs { targetUnit = targetUnit, healingUnit = unit });

        yield return new WaitForSeconds(0.5f);

        ActionComplete();
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        List<Unit> unitsAtPos = LevelGrid.Instance.GetUnitListAtGridPosition(gridPosition);
        if (unitsAtPos.Count == 0) return null;

        Unit candidate = unitsAtPos[0];
        // 아군이고 체력이 낮을수록 높은 우선순위
        int missingHealthPercent = 100 - Mathf.RoundToInt(candidate.GetHealthNormalized() * 100f);
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 200 + missingHealthPercent * 3 };
    }

    public int GetMaxHealDistance() => maxHealDistance;
    public int GetHealAmount() => healAmount;
}
