using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지정한 위치 주변 범위에 N턴 동안 유지되는 장판을 설치한다.
/// 배치 즉시 + 진입 시 + 매 턴 시작 시 범위 내 적에게 피해를 입히고 둔화·약화 디버프를 부여한다.
/// 애니메이션 없이 사용 즉시 2초 딜레이만 발생한다.
/// 시전 시 영향 범위에 빨간 외곽선을 표시한다(BlizzardAction과 동일한 방식) — 장판이 끝나면 같이 사라진다.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class PlagueCloudAction : BaseAction
{
    protected override string DefaultActionName() => "역병 구름";
    public override ActionCategory GetActionCategory() => ActionCategory.Attack;

    [Header("Range")]
    [SerializeField] private int maxRange = 7;
    [SerializeField] private int cloudRadius = 1; // 효과 반경 (칸)

    [Header("Damage")]
    [SerializeField] private int initialDamage = 10; // 배치/진입 즉시 피해
    [SerializeField] private int tickDamage = 6;      // 매 턴 시작 시 피해

    [Header("지속")]
    [SerializeField] private int duration = 3; // 장판 유지 턴 수

    [Header("둔화")]
    [SerializeField] private float slowValue = 0.3f;
    [SerializeField] private int slowDuration = 2;

    [Header("약화")]
    [SerializeField] private float weakenValue = 0.3f;
    [SerializeField] private int weakenDuration = 2;

    [Header("References")]
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private float targetingYAxis = 0.5f;
    [Tooltip("PlagueZone 프리팹. 비워두면 빈 오브젝트로 자동 생성된다.")]
    [SerializeField] private Transform zonePrefab;

    [Header("이펙트")]
    [Tooltip("장판이 지속되는 동안 함께 떠 있다가, 장판이 끝나면(PlagueZone의 자식이므로) 같이 사라진다.")]
    [SerializeField] private GameObject cloudVFXPrefab;
    [Tooltip("스폰 위치 보정값. Y로 높이를 조절한다.")]
    [SerializeField] private Vector3 cloudVFXOffset = new Vector3(0f, 0.5f, 0f);

    [Header("외곽선")]
    [Tooltip("장판이 끝날 때까지 표시된다.")]
    [SerializeField] private float outlineWidth = 0.08f;
    [SerializeField] private Color outlineColor = new Color(0.6f, 0.1f, 0.1f, 0.9f);
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private float outlineHeightOffset = 0.1f;

    private List<GridPosition> cachedValidList;
    private bool isCacheDirty = true;

    private LineRenderer lineRenderer;

    // 이 유닛의 턴마다 최대 1번만 사용 가능
    private bool usedThisTurn = false;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;

        lineRenderer = GetComponent<LineRenderer>();
        GridOutlineUtil.SetupLineRenderer(lineRenderer, outlineWidth, outlineColor, outlineMaterial);
        lineRenderer.enabled = false;
    }

    private void Start()
    {
        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
        BaseAction.OnAnyActionEnded += (s, e) => isCacheDirty = true;
    }

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        isCacheDirty = true;
        // 이 유닛의 턴이 새로 시작되면 사용 횟수 제한을 리셋
        if (TurnSystem.Instance.GetTurnUnit() == unit)
            usedThisTurn = false;
    }

    public override string GetDescription()
    {
        int atk = unit.GetAttackPower();
        return $"지정한 위치 반경 {cloudRadius}칸에 역병 구름을 일으킨다. 설치/진입 시 {initialDamage + atk} 피해, " +
               $"매 턴 시작 시 {tickDamage + atk} 피해를 입히고 둔화 {Mathf.RoundToInt(slowValue * 100)}%, " +
               $"약화 {Mathf.RoundToInt(weakenValue * 100)}%를 부여한다. (구름 {duration}턴 지속)";
    }

    // ─── 범위 타일 ────────────────────────────────────────────────────

    private List<GridPosition> GetCloudArea(GridPosition center)
    {
        List<GridPosition> list = new List<GridPosition>();
        for (int x = -cloudRadius; x <= cloudRadius; x++)
        {
            for (int z = -cloudRadius; z <= cloudRadius; z++)
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
        // 이 턴에 이미 사용했으면 재사용 불가
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
        return GetCloudArea(targetGridPosition);
    }

    // ─── 액션 실행 ────────────────────────────────────────────────────

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        usedThisTurn = true;
        ActionStart(onActionComplete);
        ShowOutline(gridPosition);
        SpawnZone(gridPosition);
        StartCoroutine(DelayRoutine());
    }

    /// <summary>영향 범위 외곽선을 표시한다. PlagueZone이 끝날 때(OnZoneEnded) 꺼진다.</summary>
    private void ShowOutline(GridPosition center)
    {
        HashSet<Vector2Int> tileSet = new HashSet<Vector2Int>();
        foreach (GridPosition pos in GetCloudArea(center))
            tileSet.Add(new Vector2Int(pos.x, pos.z));

        List<Vector2Int> path = GridOutlineUtil.BuildOutlinePath(tileSet);
        GridOutlineUtil.ApplyPathToLineRenderer(lineRenderer, path, outlineHeightOffset);
        lineRenderer.enabled = true;
    }

    private void HideOutline(object sender, EventArgs e)
    {
        if (lineRenderer != null) lineRenderer.enabled = false;
    }

    private IEnumerator DelayRoutine()
    {
        // 애니메이션 없이 사용 즉시 2초 딜레이만 발생
        yield return new WaitForSeconds(2f);
        ActionComplete();
    }

    private void SpawnZone(GridPosition center)
    {
        List<GridPosition> zonePositions = GetCloudArea(center);
        Vector3 spawnPos = LevelGrid.Instance.GetWorldPosition(center) + Vector3.up * 0.2f;

        PlagueZone zone;
        if (zonePrefab != null)
        {
            Transform inst = Instantiate(zonePrefab, spawnPos, Quaternion.identity);
            zone = inst.GetComponent<PlagueZone>();
            if (zone == null) zone = inst.gameObject.AddComponent<PlagueZone>();
        }
        else
        {
            GameObject tempObj = new GameObject("PlagueZone");
            tempObj.transform.position = spawnPos;
            zone = tempObj.AddComponent<PlagueZone>();
        }

        // 장판의 자식으로 붙여서, 장판이 파괴될 때 이펙트도 자동으로 같이 사라지게 한다.
        if (cloudVFXPrefab != null)
        {
            Vector3 vfxPos = LevelGrid.Instance.GetWorldPosition(center) + cloudVFXOffset;
            Instantiate(cloudVFXPrefab, vfxPos, Quaternion.identity, zone.transform);
        }

        // 장판이 지속 턴을 다 쓰고 끝나면 외곽선도 같이 꺼지도록 구독
        zone.OnZoneEnded += HideOutline;

        zone.Setup(zonePositions, initialDamage, tickDamage, duration, unit.GetTeamType(),
                   slowValue, slowDuration, weakenValue, weakenDuration, unit.GetAttackPower());
    }

    // ─── AI ──────────────────────────────────────────────────────────

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        int hitCount = 0;
        foreach (GridPosition pos in GetCloudArea(gridPosition))
        {
            if (!LevelGrid.Instance.IsGridPositionOccupied(pos)) continue;
            Unit target = LevelGrid.Instance.GetUnitListAtGridPosition(pos)[0];
            if (TeamHelper.IsHostile(unit.GetTeamType(), target.GetTeamType()))
                hitCount++;
        }
        return hitCount > 0
            ? new EnemyAIAction { gridPosition = gridPosition, actionValue = 50 + hitCount * 40 }
            : null;
    }
}
