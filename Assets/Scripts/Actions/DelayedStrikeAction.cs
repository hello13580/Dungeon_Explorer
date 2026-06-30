using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지정한 위치에 표식을 남기고, N턴 뒤에 그 자리에 떨어져 범위 내 적에게 피해를 입히는 지연 폭발 스킬.
/// 영향 범위를 빨간 외곽선으로 표시하고, 범위 중앙에 남은 턴 수를 숫자로 띄운다.
/// </summary>
public class DelayedStrikeAction : BaseAction
{
    protected override string DefaultActionName() => "운석 낙하";
    public override ActionCategory GetActionCategory() => ActionCategory.Attack;

    [Header("Range")]
    [SerializeField] private int maxRange = 7;
    [SerializeField] private int strikeRadius = 1; // 효과 반경 (칸)

    [Header("Damage")]
    [SerializeField] private int damage = 35;

    [Header("지연")]
    [SerializeField] private int delayTurns = 2; // 시전 후 몇 턴 뒤에 떨어지는지

    [Header("References")]
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private float targetingYAxis = 0.5f;
    [Tooltip("DelayedStrikeZone 프리팹. 비워두면 빈 오브젝트로 자동 생성된다.")]
    [SerializeField] private Transform zonePrefab;

    [Header("외곽선")]
    [SerializeField] private float outlineWidth = 0.08f;
    [SerializeField] private Color outlineColor = new Color(1f, 0.2f, 0.2f, 0.9f);
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private float outlineHeightOffset = 0.1f;

    [Header("카운트다운 숫자")]
    [SerializeField] private float textHeightOffset = 1.5f;
    [SerializeField] private Color textColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private float textFontSize = 12f;

    private List<GridPosition> cachedValidList;
    private bool isCacheDirty = true;

    // 이 유닛의 턴마다 최대 1번만 사용 가능
    private bool usedThisTurn = false;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }

    private void Start()
    {
        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
        BaseAction.OnAnyActionEnded += (s, e) => isCacheDirty = true;
    }

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        isCacheDirty = true;
        if (TurnSystem.Instance.GetTurnUnit() == unit)
            usedThisTurn = false;
    }

    public override string GetDescription()
    {
        int atk = unit.GetAttackPower();
        return $"지정한 위치 반경 {strikeRadius}칸에 표식을 남긴다. {delayTurns}턴 뒤, " +
               $"그 범위 내 모든 적에게 {damage + atk} 피해를 입힌다.";
    }

    // ─── 범위 타일 ────────────────────────────────────────────────────

    private List<GridPosition> GetStrikeArea(GridPosition center)
    {
        List<GridPosition> list = new List<GridPosition>();
        for (int x = -strikeRadius; x <= strikeRadius; x++)
        {
            for (int z = -strikeRadius; z <= strikeRadius; z++)
            {
                GridPosition pos = new GridPosition(center.x + x, center.z + z, center.floor);
                if (LevelGrid.Instance.IsValidGridPosition(pos))
                    list.Add(pos);
            }
        }
        return list;
    }

    // ─── 유효 액션 위치 — 적 점유 여부와 무관하게 사거리 내 모든 칸 ────

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        if (usedThisTurn) return new List<GridPosition>();

        if (!isCacheDirty && cachedValidList != null) return cachedValidList;

        GridPosition unitPos = unit.GetGridPosition();
        int floorAmount = LevelGrid.Instance.GetFloorAmount();
        int minFloor = Mathf.Clamp(unitPos.floor - maxRange, 0, floorAmount - 1);
        int maxFloor = Mathf.Clamp(unitPos.floor + maxRange, 0, floorAmount - 1);

        cachedValidList = new List<GridPosition>();

        for (int x = -maxRange; x <= maxRange; x++)
        {
            for (int z = -maxRange; z <= maxRange; z++)
            {
                if (Mathf.Sqrt(x * x + z * z) > maxRange) continue;

                for (int floor = minFloor; floor <= maxFloor; floor++)
                {
                    GridPosition testPos = new GridPosition(unitPos.x + x, unitPos.z + z, floor);
                    if (!LevelGrid.Instance.IsValidGridPosition(testPos)) continue;
                    if (!PathFinding.Instance.IsDirectlyTargetable(testPos)) continue;

                    if (obstacleLayerMask != 0)
                    {
                        Vector3 startPos = unit.GetWorldPosition() + Vector3.up * (unit.GetCollider().bounds.size.y * 0.75f);
                        Vector3 targetPos = LevelGrid.Instance.GetWorldPosition(testPos) + Vector3.up * targetingYAxis;
                        if (Physics.Raycast(startPos, (targetPos - startPos).normalized, Vector3.Distance(startPos, targetPos), obstacleLayerMask))
                            continue;
                    }

                    cachedValidList.Add(testPos);
                }
            }
        }

        isCacheDirty = false;
        return cachedValidList;
    }

    public override List<GridPosition> GetActionRangeGridPositionList()
    {
        return GetValidActionGridPositionList();
    }

    public override List<GridPosition> GetDamageAffectedGridPosition(GridPosition targetGridPosition)
    {
        return GetStrikeArea(targetGridPosition);
    }

    // ─── 액션 실행 ────────────────────────────────────────────────────

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        usedThisTurn = true;
        ActionStart(onActionComplete);
        SpawnZone(gridPosition);
        StartCoroutine(DelayRoutine());
    }

    private IEnumerator DelayRoutine()
    {
        yield return new WaitForSeconds(0.3f);
        ActionComplete();
    }

    private void SpawnZone(GridPosition center)
    {
        List<GridPosition> zonePositions = GetStrikeArea(center);
        Vector3 spawnPos = LevelGrid.Instance.GetWorldPosition(center) + Vector3.up * 0.2f;

        DelayedStrikeZone zone;
        if (zonePrefab != null)
        {
            Transform inst = Instantiate(zonePrefab, spawnPos, Quaternion.identity);
            zone = inst.GetComponent<DelayedStrikeZone>();
            if (zone == null) zone = inst.gameObject.AddComponent<DelayedStrikeZone>();
        }
        else
        {
            GameObject tempObj = new GameObject("DelayedStrikeZone");
            tempObj.transform.position = spawnPos;
            zone = tempObj.AddComponent<DelayedStrikeZone>();
        }

        zone.Setup(zonePositions, damage, delayTurns, unit.GetTeamType(), unit.GetAttackPower(),
                   outlineWidth, outlineColor, outlineMaterial, outlineHeightOffset,
                   textHeightOffset, textColor, textFontSize);
    }

    // ─── AI ──────────────────────────────────────────────────────────

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        int hitCount = 0;
        foreach (GridPosition pos in GetStrikeArea(gridPosition))
        {
            if (!LevelGrid.Instance.IsGridPositionOccupied(pos)) continue;
            Unit target = LevelGrid.Instance.GetUnitListAtGridPosition(pos)[0];
            if (TeamHelper.IsHostile(unit.GetTeamType(), target.GetTeamType()))
                hitCount++;
        }
        return hitCount > 0
            ? new EnemyAIAction { gridPosition = gridPosition, actionValue = 60 + hitCount * 40 }
            : null;
    }
}
