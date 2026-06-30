using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지정한 위치 주변 범위에 N턴 동안 유지되는 눈보라 장판을 설치한다.
/// 배치 즉시 + 진입 시 + 매 턴 시작 시 범위 내 적에게 피해를 입히고 이동 거리 감소(둔화) 디버프를 부여한다.
/// PersistentAOEAction(화염 장판)과 동일한 시전 흐름이며, 실제 장판 동작은 BlizzardZone이 담당한다.
/// 시전 시 영향 범위에 외곽선을 일정 시간 표시한다(WhirlwindAction과 동일한 방식).
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class BlizzardAction : BaseAction
{
    protected override string DefaultActionName() => "눈보라";
    public override ActionCategory GetActionCategory() => ActionCategory.Attack;

    [Header("Range")]
    [SerializeField] private int maxRange = 7;
    [SerializeField] private int blizzardRadius = 1; // 효과 반경 (칸)

    [Header("Damage")]
    [SerializeField] private int initialDamage = 18; // 배치/진입 즉시 피해
    [SerializeField] private int tickDamage = 10;     // 매 턴 시작 시 피해

    [Header("지속")]
    [SerializeField] private int duration = 2; // 장판 유지 턴 수

    [Header("Slow Debuff")]
    [SerializeField] private float slowValue = 0.3f;
    [SerializeField] private int slowDuration = 2;

    [Header("References")]
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private float targetingYAxis = 0.5f;
    [Tooltip("BlizzardZone 프리팹. 비워두면 빈 오브젝트로 자동 생성된다.")]
    [SerializeField] private Transform zonePrefab;

    [Header("Impact VFX")]
    [Tooltip("눈보라 장판이 지속되는 동안 함께 떠 있다가, 장판이 끝나면(BlizzardZone의 자식이므로) 같이 사라진다.")]
    [SerializeField] private GameObject impactVFXPrefab;
    [Tooltip("스폰 위치 보정값. Y는 하늘에서 고드름이 떨어지는 등 위에서 시작하는 높이, X/Z는 이펙트 원점이 실제 범위 중심과 어긋날 때 맞추는 용도")]
    [SerializeField] private Vector3 impactVFXOffset = new Vector3(0f, 5f, 0f);

    [Header("Cast VFX")]
    [Tooltip("시전 시작 시 한 번 스폰되는 이펙트. 유닛(영속 오브젝트)에 부모로 붙이지 않고 독립적으로 스폰한다.")]
    [SerializeField] private Transform castVFXPrefab;
    [SerializeField] private Transform castVFXSpawnPoint;
    [SerializeField] private float castVFXLifetime = 1.5f;

    [Header("외곽선")]
    [Tooltip("눈보라 장판이 끝날 때까지 표시된다.")]
    [SerializeField] private float outlineWidth = 0.08f;
    [SerializeField] private Color outlineColor = new Color(0.4f, 0.8f, 1f, 0.9f);
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private float outlineHeightOffset = 0.1f;

    public event EventHandler OnCastStarted;

    private List<GridPosition> cachedValidList;
    private bool isCacheDirty = true;

    private GridPosition pendingTargetPosition;
    private bool blizzardCast = false;

    private LineRenderer lineRenderer;

    protected override void Awake()
    {
        base.Awake();

        lineRenderer = GetComponent<LineRenderer>();
        GridOutlineUtil.SetupLineRenderer(lineRenderer, outlineWidth, outlineColor, outlineMaterial);
        lineRenderer.enabled = false;
    }

    private void Start()
    {
        TurnSystem.Instance.OnTurnChanged += (s, e) => isCacheDirty = true;
        BaseAction.OnAnyActionEnded += (s, e) => isCacheDirty = true;
    }

    /// <summary>
    /// 시전 시작 시 한 번 스폰되는 이펙트.
    /// [버그 수정] 파티 유닛은 DontDestroyOnLoad로 유지되는 영속 오브젝트라서, 그걸 부모로
    /// Instantiate(prefab, pos, rot, parent)를 호출하면 유니티가 부모 지정을 거부하고
    /// "Cannot instantiate objects with a parent which is persistent" 경고를 띄운다.
    /// 부모 없이 스폰하고 일정 시간 후 자동 파괴하는 것으로 충분하므로 부모 지정을 하지 않는다.
    /// </summary>
    private void SpawnCastVFX()
    {
        if (castVFXPrefab == null) return;
        Transform spawnPoint = castVFXSpawnPoint != null ? castVFXSpawnPoint : unit.transform;
        Transform vfx = Instantiate(castVFXPrefab, spawnPoint.position, spawnPoint.rotation);
        Destroy(vfx.gameObject, castVFXLifetime);
    }

    public override string GetDescription()
    {
        int atk = unit.GetAttackPower();
        return $"지정한 위치 반경 {blizzardRadius}칸에 눈보라를 일으킨다. 설치/진입 시 {initialDamage + atk} 피해, " +
               $"매 턴 시작 시 {tickDamage + atk} 피해를 입히고 이동 거리를 {Mathf.RoundToInt(slowValue * 100)}% 감소시킨다. " +
               $"(눈보라 {duration}턴 지속, 둔화 {slowDuration}턴)";
    }

    // ─── 범위 타일 ────────────────────────────────────────────────────

    private List<GridPosition> GetZoneArea(GridPosition center)
    {
        List<GridPosition> list = new List<GridPosition>();
        for (int x = -blizzardRadius; x <= blizzardRadius; x++)
        {
            for (int z = -blizzardRadius; z <= blizzardRadius; z++)
            {
                GridPosition pos = new GridPosition(center.x + x, center.z + z, center.floor);
                if (LevelGrid.Instance.IsValidGridPosition(pos))
                    list.Add(pos);
            }
        }
        return list;
    }

    // ─── 유효 액션 위치 ───────────────────────────────────────────────

    public override List<GridPosition> GetValidActionGridPositionList()
    {
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
        return GetZoneArea(targetGridPosition);
    }

    // ─── 액션 실행 ────────────────────────────────────────────────────

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        pendingTargetPosition = gridPosition;
        blizzardCast = false;
        SpawnCastVFX();
        ShowOutline(gridPosition);
        ActionStart(onActionComplete);
        StartCoroutine(CastRoutine(gridPosition));
    }

    /// <summary>영향 범위 외곽선을 표시한다. BlizzardZone이 끝날 때(OnZoneEnded) 꺼진다.</summary>
    private void ShowOutline(GridPosition center)
    {
        HashSet<Vector2Int> tileSet = new HashSet<Vector2Int>();
        foreach (GridPosition pos in GetZoneArea(center))
            tileSet.Add(new Vector2Int(pos.x, pos.z));

        List<Vector2Int> path = GridOutlineUtil.BuildOutlinePath(tileSet);
        GridOutlineUtil.ApplyPathToLineRenderer(lineRenderer, path, outlineHeightOffset);
        lineRenderer.enabled = true;
    }

    private void HideOutline(object sender, EventArgs e)
    {
        if (lineRenderer != null) lineRenderer.enabled = false;
    }

    /// <summary>애니메이션 이벤트에서 AnimationEventRelay.SpawnBlizzard()를 통해 호출됨.</summary>
    public void SpawnBlizzardFromAnimation()
    {
        SpawnZone(pendingTargetPosition);
        blizzardCast = true;
    }

    private IEnumerator CastRoutine(GridPosition targetGridPosition)
    {
        // 타겟 방향으로 회전 (최대 1초 타임아웃)
        Vector3 aimPos = LevelGrid.Instance.GetWorldPosition(targetGridPosition);
        aimPos.y = transform.position.y;
        Vector3 aimDir = aimPos - transform.position;
        if (aimDir.sqrMagnitude > 0.01f)
        {
            aimDir.Normalize();
            float timeout = 0f;
            while (timeout < 1f && Vector3.Angle(transform.forward, aimDir) > 1f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(aimDir),
                    Time.deltaTime * 15f);
                timeout += Time.deltaTime;
                yield return null;
            }
            transform.rotation = Quaternion.LookRotation(aimDir);
        }

        OnCastStarted?.Invoke(this, EventArgs.Empty);

        // 애니메이션 이벤트(SpawnBlizzard)가 SpawnBlizzardFromAnimation()을 호출할 때까지 대기.
        // 이벤트가 없을 경우를 대비해 2초 안전 타임아웃을 둔다.
        float castTimeout = 2f;
        while (!blizzardCast && castTimeout > 0f)
        {
            castTimeout -= Time.deltaTime;
            yield return null;
        }
        if (!blizzardCast) SpawnZone(pendingTargetPosition);

        yield return new WaitForSeconds(0.3f);
        ActionComplete();
    }

    private void SpawnZone(GridPosition center)
    {
        List<GridPosition> zonePositions = GetZoneArea(center);
        Vector3 spawnPos = LevelGrid.Instance.GetWorldPosition(center) + Vector3.up * 0.2f;

        BlizzardZone zone;
        if (zonePrefab != null)
        {
            Transform inst = Instantiate(zonePrefab, spawnPos, Quaternion.identity);
            zone = inst.GetComponent<BlizzardZone>();
            if (zone == null) zone = inst.gameObject.AddComponent<BlizzardZone>();
        }
        else
        {
            GameObject tempObj = new GameObject("BlizzardZone");
            tempObj.transform.position = spawnPos;
            zone = tempObj.AddComponent<BlizzardZone>();
        }

        // 장판의 자식으로 붙여서, 장판이 파괴될 때 이펙트도 자동으로 같이 사라지게 한다.
        if (impactVFXPrefab != null)
        {
            Vector3 vfxPos = LevelGrid.Instance.GetWorldPosition(center) + impactVFXOffset;
            Instantiate(impactVFXPrefab, vfxPos, Quaternion.identity, zone.transform);
        }

        // 장판이 지속 턴을 다 쓰고 끝나면 아웃라인도 같이 꺼지도록 구독
        zone.OnZoneEnded += HideOutline;

        zone.Setup(zonePositions, initialDamage, tickDamage, duration, unit.GetTeamType(),
                   slowValue, slowDuration, unit.GetAttackPower());
    }

    // ─── AI ──────────────────────────────────────────────────────────

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        int hitCount = 0;
        foreach (GridPosition pos in GetZoneArea(gridPosition))
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
