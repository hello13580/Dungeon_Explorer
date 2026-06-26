using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 범위를 지정해 장판을 설치하는 액션.
/// 설치 즉시 피해를 주고 이후 매 턴 시작마다 범위 내 적에게 틱 피해를 준다.
/// </summary>
public class PersistentAOEAction : BaseAction
{
    [Header("Range")]
    [SerializeField] private int maxRange = 7;
    [SerializeField] private int zoneRadius = 1;          // 장판 반경 (칸)

    [Header("Damage")]
    [SerializeField] private int initialDamage = 15;      // 배치 즉시 피해
    [SerializeField] private int tickDamage = 8;          // 매 턴 틱 피해

    [Header("Duration")]
    [SerializeField] private int duration = 3;            // 지속 턴 수

    [Header("References")]
    [SerializeField] private Transform damageZonePrefab;  // DamageZone 프리팹
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private float targetingYAxis = 0.5f;

    [Header("Cast VFX")]
    [SerializeField] private Transform castVFXPrefab;
    [SerializeField] private Transform castVFXSpawnPoint;
    [SerializeField] private float castVFXHideDelay = 1.5f;

    public event EventHandler OnCastStarted;

    // 유효 위치 캐시
    private List<GridPosition> cachedValidList;
    private bool isCacheDirty = true;

    private GridPosition pendingTargetPosition;
    private bool zoneSpawned = false;
    private GameObject activeCastVFX;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }

    private void Start()
    {
        TurnSystem.Instance.OnTurnChanged += (s, e) => isCacheDirty = true;
        BaseAction.OnAnyActionEnded += (s, e) => isCacheDirty = true;
        UnitActionSystem.Instance.OnSelectedActionChanged += OnSelectedActionChanged;
    }

    private void OnDestroy()
    {
        if (UnitActionSystem.Instance != null)
            UnitActionSystem.Instance.OnSelectedActionChanged -= OnSelectedActionChanged;
        HideCastVFX();
    }

    private void OnSelectedActionChanged(object sender, BaseAction selectedAction)
    {
        if (selectedAction == this)
            ShowCastVFX();
        else
            HideCastVFX();
    }

    private void ShowCastVFX()
    {
        if (castVFXPrefab == null || activeCastVFX != null) return;
        Transform spawnPoint = castVFXSpawnPoint != null ? castVFXSpawnPoint : unit.transform;
        activeCastVFX = Instantiate(castVFXPrefab, spawnPoint.position, spawnPoint.rotation, spawnPoint).gameObject;
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

    public override string GetActionName() => "Zone";
    public override string GetDescription()
    {
        int atk = unit.GetAttackPower();
        return $"지정한 위치에 화염 장판을 설치한다. 설치 시 {initialDamage + atk} 피해, 매 턴 시작 시 {tickDamage + atk} 피해. ({duration}턴 지속, 반경 {zoneRadius}칸)";
    }


    // ─── 범위 타일 ────────────────────────────────────────────────────

    private List<GridPosition> GetZoneArea(GridPosition center)
    {
        List<GridPosition> list = new List<GridPosition>();
        for (int x = -zoneRadius; x <= zoneRadius; x++)
        {
            for (int z = -zoneRadius; z <= zoneRadius; z++)
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

                    // 장애물 시야 체크
                    if (obstacleLayerMask != 0)
                    {
                        float height = unit.GetCollider().bounds.size.y * 0.75f;
                        Vector3 start = unit.GetWorldPosition() + Vector3.up * height;
                        Vector3 end = LevelGrid.Instance.GetWorldPosition(testPos) + Vector3.up * targetingYAxis;
                        if (Physics.Raycast(start, (end - start).normalized, Vector3.Distance(start, end), obstacleLayerMask))
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
        zoneSpawned = false;
        HideCastVFXDelayed();
        ActionStart(onActionComplete);
        StartCoroutine(CastRoutine(gridPosition));
    }

    /// <summary>애니메이션 이벤트에서 AnimationEventRelay.SpawnFireZone()을 통해 호출됨.</summary>
    public void SpawnZoneFromAnimation()
    {
        SpawnZone(pendingTargetPosition);
        zoneSpawned = true;
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

        // 애니메이션 이벤트(SpawnFireZone)가 SpawnZoneFromAnimation()을 호출할 때까지 대기
        yield return new WaitUntil(() => zoneSpawned);

        yield return new WaitForSeconds(0.3f);
        ActionComplete();
    }

    private void SpawnZone(GridPosition targetGridPosition)
    {
        List<GridPosition> zonePositions = GetZoneArea(targetGridPosition);
        Vector3 spawnPos = LevelGrid.Instance.GetWorldPosition(targetGridPosition) + Vector3.up * 0.2f;

        DamageZone zone;

        if (damageZonePrefab != null)
        {
            Transform inst = Instantiate(damageZonePrefab, spawnPos, Quaternion.identity);
            zone = inst.GetComponent<DamageZone>();
            if (zone == null) zone = inst.gameObject.AddComponent<DamageZone>();
        }
        else
        {
            GameObject tempObj = new GameObject("DamageZone");
            tempObj.transform.position = spawnPos;
            zone = tempObj.AddComponent<DamageZone>();
        }

        // 시전자 공격력을 Setup에 넘겨 장판 피해에 반영
        zone.Setup(zonePositions, initialDamage, tickDamage, duration, unit.GetTeamType(), unit.GetAttackPower());
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
            ? new EnemyAIAction { gridPosition = gridPosition, actionValue = 30 + hitCount * 20 }
            : null;
    }
}
