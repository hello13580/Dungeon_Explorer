using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사거리 내 적에게 피해를 입히고 약화(DamageReduce) 디버프를 부여하는 스킬.
/// </summary>
public class WeakenStrikeAction : BaseAction
{
    protected override string DefaultActionName() => "약화의 일격";
    public override ActionCategory GetActionCategory() => ActionCategory.Attack;

    [SerializeField] private int maxRange = 5;
    [SerializeField] private int damage = 12;
    [SerializeField] private float hitForce = 400f;
    [SerializeField] private LayerMask obstacleLayerMask;

    [Header("약화 디버프")]
    [SerializeField] private float weakenValue = 0.3f; // 주는 피해 감소 비율
    [SerializeField] private int weakenDuration = 2;

    [Header("이펙트")]
    [SerializeField] private GameObject debuffEffectPrefab;
    [SerializeField] private float debuffEffectHeightOffset = 0.3f;
    [SerializeField] private float debuffEffectLifetime = 2f;

    private Unit targetUnit;
    private float rotateSpeed = 10f;

    private List<GridPosition> cachedValidGridPositionList;
    private bool isCacheDirty = true;

    public static event EventHandler<OnStrikeEventArgs> OnAnyWeakenStrike;
    public event EventHandler<OnStrikeEventArgs> OnWeakenStrike;

    public class OnStrikeEventArgs : EventArgs
    {
        public Unit targetUnit;
        public Unit attackingUnit;
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
        $"사거리 {maxRange} 내 적에게 {damage + unit.GetAttackPower()} 피해를 입히고 약화를 부여한다.\n" +
        $"주는 피해 {Mathf.RoundToInt(weakenValue * 100)}% 감소. ({weakenDuration}턴)";

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
                        if (!TeamHelper.IsHostile(unit.GetTeamType(), candidate.GetTeamType())) continue;
                        if (candidate.IsStealthed()) continue;
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
            Debug.LogError("약화의 일격을 쓰려는데 타겟 없음: " + gridPosition.ToString());
            return;
        }

        targetUnit = targetUnitList[0];
        ActionStart(onActionComplete);
        StartCoroutine(StrikeRoutine());
    }

    private IEnumerator StrikeRoutine()
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

        Vector3 hitDir = (targetUnit.GetWorldPosition() - unit.GetWorldPosition()).normalized;
        targetUnit.GetHitReaction().SetHitDirection(hitDir);
        targetUnit.GetHitReaction().SetHitForce(hitForce);
        targetUnit.Damage(unit.CalculateDamage(damage));

        StatusEffectSystem ses = targetUnit.GetComponent<StatusEffectSystem>();
        ses?.AddEffect(new StatusEffect(
            StatusEffectType.DamageReduce, weakenValue, weakenDuration,
            "약화", StackingMode.RefreshDuration));

        OnWeakenStrike?.Invoke(this, new OnStrikeEventArgs { targetUnit = targetUnit, attackingUnit = unit });
        OnAnyWeakenStrike?.Invoke(this, new OnStrikeEventArgs { targetUnit = targetUnit, attackingUnit = unit });
        SpawnDebuffEffect(targetUnit);

        yield return new WaitForSeconds(0.5f);

        ActionComplete();
    }

    private void SpawnDebuffEffect(Unit target)
    {
        if (debuffEffectPrefab == null) return;

        float headY = target.GetCollider().bounds.max.y + debuffEffectHeightOffset;
        Vector3 spawnPos = new Vector3(target.GetWorldPosition().x, headY, target.GetWorldPosition().z);

        GameObject effect = Instantiate(debuffEffectPrefab, spawnPos, Quaternion.identity);
        Destroy(effect, debuffEffectLifetime);
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        Unit target = LevelGrid.Instance.GetUnitListAtGridPosition(gridPosition)[0];

        StatusEffectSystem ses = target.GetComponent<StatusEffectSystem>();
        bool alreadyWeakened = ses != null && ses.HasEffect(StatusEffectType.DamageReduce);

        int currentHealth = target.GetCurrentHealth();
        int value = 900 + (500 - currentHealth) - (alreadyWeakened ? 400 : 0);
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = value };
    }
}
