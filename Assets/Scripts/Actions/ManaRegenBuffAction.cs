using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사거리 내 아군에게 마나 재생력 증가 버프를 거는 클레릭용 스킬.
/// 대상의 턴마다 회복하는 마나가 N만큼 M턴 동안 증가한다. HealAction과 동일한 시전 흐름을 쓴다.
/// </summary>
public class ManaRegenBuffAction : BaseAction
{
    protected override string DefaultActionName() => "마나 축복";

    [SerializeField] private int maxRange = 5;
    [SerializeField] private int regenAmount = 3;
    [SerializeField] private int duration = 3;
    [SerializeField] private LayerMask obstacleLayerMask;

    [Header("이펙트")]
    [SerializeField] private GameObject buffEffectPrefab;
    [Tooltip("유닛 머리 위 기준 추가 높이 오프셋")]
    [SerializeField] private float effectHeightOffset = 0.3f;
    [SerializeField] private float effectLifetime = 2f;

    private Unit targetUnit;
    private float rotateSpeed = 10f;

    private List<GridPosition> cachedValidGridPositionList;
    private bool isCacheDirty = true;

    public static event EventHandler<OnBuffEventArgs> OnAnyManaRegenBuff;
    public event EventHandler<OnBuffEventArgs> OnManaRegenBuff;

    public class OnBuffEventArgs : EventArgs
    {
        public Unit targetUnit;
        public Unit castingUnit;
    }

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
        $"사거리 {maxRange} 내 아군의 마나 재생력을 {regenAmount}만큼 {duration}턴 동안 증가시킨다.";

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        if (!isCacheDirty && cachedValidGridPositionList != null) return cachedValidGridPositionList;

        cachedValidGridPositionList = new List<GridPosition>();
        GridPosition unitGridPosition = unit.GetGridPosition();

        int floorAmount = LevelGrid.Instance.GetFloorAmount();
        int minFloor = Mathf.Clamp(unitGridPosition.floor - maxRange, 0, floorAmount - 1);
        int maxFloor = Mathf.Clamp(unitGridPosition.floor + maxRange, 0, floorAmount - 1);

        for (int x = -maxRange; x <= maxRange; x++)
        {
            for (int z = -maxRange; z <= maxRange; z++)
            {
                float sizeOffset = (unit.GetSize() - 1) * 0.5f;
                float distX = x - sizeOffset;
                float distZ = z - sizeOffset;
                if (Mathf.Sqrt(distX * distX + distZ * distZ) > maxRange) continue;

                for (int floor = minFloor; floor <= maxFloor; floor++)
                {
                    GridPosition testGridPosition = new GridPosition(unitGridPosition.x + x, unitGridPosition.z + z, floor);
                    if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;
                    if (!LevelGrid.Instance.IsGridPositionOccupied(testGridPosition)) continue;

                    foreach (Unit candidate in LevelGrid.Instance.GetUnitListAtGridPosition(testGridPosition))
                    {
                        if (TeamHelper.IsHostile(unit.GetTeamType(), candidate.GetTeamType())) continue;
                        if (candidate.GetManaSystem() == null) continue; // 마나 없는 유닛은 대상 제외
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
        int minFloor = Mathf.Clamp(unitGridPosition.floor - maxRange, 0, floorAmount - 1);
        int maxFloor = Mathf.Clamp(unitGridPosition.floor + maxRange, 0, floorAmount - 1);

        for (int x = -maxRange; x <= maxRange; x++)
        {
            for (int z = -maxRange; z <= maxRange; z++)
            {
                float sizeOffset = (unit.GetSize() - 1) * 0.5f;
                float distX = x - sizeOffset;
                float distZ = z - sizeOffset;
                if (Mathf.Sqrt(distX * distX + distZ * distZ) > maxRange) continue;

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
            Debug.LogError("마나 축복을 걸려는데 타겟 없음: " + gridPosition.ToString());
            return;
        }

        targetUnit = targetUnitList[0];
        ActionStart(onActionComplete);
        StartCoroutine(BuffRoutine());
    }

    private IEnumerator BuffRoutine()
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

        targetUnit.GetManaSystem()?.ApplyOrRefreshRegenBuff("ManaRegenBuff", regenAmount, duration);
        OnManaRegenBuff?.Invoke(this, new OnBuffEventArgs { targetUnit = targetUnit, castingUnit = unit });
        OnAnyManaRegenBuff?.Invoke(this, new OnBuffEventArgs { targetUnit = targetUnit, castingUnit = unit });
        SpawnBuffEffect(targetUnit);

        yield return new WaitForSeconds(0.5f);

        ActionComplete();
    }

    private void SpawnBuffEffect(Unit target)
    {
        if (buffEffectPrefab == null) return;

        // 유닛 콜라이더 최상단(머리 위)을 기준으로 추가 오프셋만큼 띄워서 스폰
        float headY = target.GetCollider().bounds.max.y + effectHeightOffset;
        Vector3 spawnPos = new Vector3(target.GetWorldPosition().x, headY, target.GetWorldPosition().z);

        GameObject effect = Instantiate(buffEffectPrefab, spawnPos, Quaternion.identity);
        Destroy(effect, effectLifetime);
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        List<Unit> unitsAtPos = LevelGrid.Instance.GetUnitListAtGridPosition(gridPosition);
        if (unitsAtPos.Count == 0) return null;

        Unit candidate = unitsAtPos[0];
        // 마나가 적게 남아있을수록 높은 우선순위
        ManaSystem ms = candidate.GetManaSystem();
        int missingManaPercent = ms != null ? 100 - Mathf.RoundToInt(ms.GetManaNormalized() * 100f) : 0;
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 80 + missingManaPercent * 2 };
    }

    public int GetMaxRange() => maxRange;
    public int GetRegenAmount() => regenAmount;
}
