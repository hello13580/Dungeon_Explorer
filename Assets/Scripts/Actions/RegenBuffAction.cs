using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사거리 내 아군에게 재생 상태이상을 부여하는 클레릭용 스킬.
/// HealAction과 동일한 시전 흐름을 쓰고, 애니메이션은 HealAction과 같은 트리거(isHealing)를 공유한다.
/// </summary>
public class RegenBuffAction : BaseAction
{
    protected override string DefaultActionName() => "재생";

    [SerializeField] private int maxRange = 5;
    [SerializeField] private int regenAmount = 3;
    [SerializeField] private LayerMask obstacleLayerMask;

    [Header("부여 이펙트")]
    [Tooltip("대상 머리 위에 스폰되는 재생 부여 이펙트")]
    [SerializeField] private GameObject buffEffectPrefab;
    [SerializeField] private float buffEffectHeightOffset = 0.3f;
    [SerializeField] private float buffEffectLifetime = 2f;

    [Header("Cast VFX")]
    [Tooltip("이 액션을 선택해서 대상을 지정하는 동안 표시되는 이펙트. 영속 오브젝트(유닛)에 부모로 붙이지 않는다.")]
    [SerializeField] private Transform castVFXPrefab;
    [SerializeField] private Transform castVFXSpawnPoint;
    [SerializeField] private float castVFXHideDelay = 1.5f;

    private Unit targetUnit;
    private float rotateSpeed = 10f;
    private GameObject activeCastVFX;

    private List<GridPosition> cachedValidGridPositionList;
    private bool isCacheDirty = true;

    public static event EventHandler<OnBuffEventArgs> OnAnyRegenBuff;
    public event EventHandler<OnBuffEventArgs> OnRegenBuff;

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
        UnitActionSystem.Instance.OnSelectedActionChanged += OnSelectedActionChanged;
    }

    private void OnDestroy()
    {
        TurnSystem.Instance.OnTurnChanged -= OnCacheInvalidated;
        BaseAction.OnAnyActionEnded -= OnCacheInvalidated;
        if (UnitActionSystem.Instance != null)
            UnitActionSystem.Instance.OnSelectedActionChanged -= OnSelectedActionChanged;
        HideCastVFX();
    }

    private void OnCacheInvalidated(object sender, EventArgs e) => isCacheDirty = true;

    public override string GetDescription() =>
        $"사거리 {maxRange} 내 아군에게 재생을 {regenAmount} 부여한다.";

    // ─── 시전 이펙트 (대상 지정 중 표시) ─────────────────────────────────

    private void OnSelectedActionChanged(object sender, BaseAction selectedAction)
    {
        if (selectedAction == this)
            ShowCastVFX();
        else
            HideCastVFX();
    }

    /// <summary>유닛(영속 오브젝트)에 부모로 붙이면 "Cannot instantiate objects with a parent
    /// which is persistent" 경고가 뜨므로, 부모 없이 스폰하고 직접 참조로 관리한다.</summary>
    private void ShowCastVFX()
    {
        if (castVFXPrefab == null || activeCastVFX != null) return;
        Transform spawnPoint = castVFXSpawnPoint != null ? castVFXSpawnPoint : unit.transform;
        activeCastVFX = Instantiate(castVFXPrefab, spawnPoint.position, spawnPoint.rotation).gameObject;
    }

    private void HideCastVFX()
    {
        if (activeCastVFX == null) return;
        Destroy(activeCastVFX);
        activeCastVFX = null;
    }

    private void HideCastVFXDelayed()
    {
        if (activeCastVFX == null) return;
        StartCoroutine(HideCastVFXRoutine());
    }

    private IEnumerator HideCastVFXRoutine()
    {
        yield return new WaitForSeconds(castVFXHideDelay);
        HideCastVFX();
    }

    // ─── 유효 액션 위치 ───────────────────────────────────────────────

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

    // ─── 액션 실행 ────────────────────────────────────────────────────

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        List<Unit> targetUnitList = LevelGrid.Instance.GetUnitListAtGridPosition(gridPosition);
        if (targetUnitList.Count == 0)
        {
            Debug.LogError("재생을 걸려는데 타겟 없음: " + gridPosition.ToString());
            return;
        }

        targetUnit = targetUnitList[0];
        HideCastVFXDelayed();
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

        StatusEffectSystem ses = targetUnit.GetComponent<StatusEffectSystem>();
        ses?.AddEffect(new StatusEffect(StatusEffectType.Regen, regenAmount, regenAmount, "재생", StackingMode.AddValue));

        OnRegenBuff?.Invoke(this, new OnBuffEventArgs { targetUnit = targetUnit, castingUnit = unit });
        OnAnyRegenBuff?.Invoke(this, new OnBuffEventArgs { targetUnit = targetUnit, castingUnit = unit });
        SpawnBuffEffect(targetUnit);

        yield return new WaitForSeconds(0.5f);

        ActionComplete();
    }

    private void SpawnBuffEffect(Unit target)
    {
        if (buffEffectPrefab == null) return;

        // 유닛 콜라이더 최상단(머리 위)을 기준으로 추가 오프셋만큼 띄워서 스폰
        float headY = target.GetCollider().bounds.max.y + buffEffectHeightOffset;
        Vector3 spawnPos = new Vector3(target.GetWorldPosition().x, headY, target.GetWorldPosition().z);

        GameObject effect = Instantiate(buffEffectPrefab, spawnPos, Quaternion.identity);
        Destroy(effect, buffEffectLifetime);
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        List<Unit> unitsAtPos = LevelGrid.Instance.GetUnitListAtGridPosition(gridPosition);
        if (unitsAtPos.Count == 0) return null;

        Unit candidate = unitsAtPos[0];
        // 이미 재생이 걸려 있으면 우선순위를 낮춘다(다른 대상 우선)
        StatusEffectSystem ses = candidate.GetComponent<StatusEffectSystem>();
        bool alreadyRegen = ses != null && ses.HasEffect(StatusEffectType.Regen);

        int missingHealthPercent = 100 - Mathf.RoundToInt(candidate.GetHealthNormalized() * 100f);
        int value = 80 + missingHealthPercent * 2 - (alreadyRegen ? 60 : 0);
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = value };
    }

    public int GetMaxRange() => maxRange;
    public int GetRegenAmount() => regenAmount;
}
