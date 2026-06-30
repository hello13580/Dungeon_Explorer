using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 마우스 방향으로 회전하는 부채꼴 범위 내 적을 밀쳐내는 액션.
/// 클릭한 타일의 방향으로 부채꼴이 확정되어 발동된다.
/// </summary>
public class WindBlastAction : BaseAction
{
    protected override string DefaultActionName() => "바람 폭발";
    public override ActionCategory GetActionCategory() => ActionCategory.Attack;
    [Header("Range")]
    [SerializeField] private int coneRange = 6;        // 부채꼴 최대 거리 (칸)
    [SerializeField] private float coneAngle = 60f;    // 부채꼴 반각 (총 각도의 절반, 도)

    [Header("Push")]
    [SerializeField] private int pushDistance = 2;
    [SerializeField] private float pushDuration = 0.35f;
    [SerializeField] private float arcHeight = 1.2f;

    [Header("Damage")]
    [SerializeField] private int blastDamage = 5;
    [SerializeField] private int collisionDamage = 10;
    [SerializeField] private float hitForce = 800f;

    [Header("Obstacle")]
    [SerializeField] private LayerMask obstacleLayerMask;

    [Header("Timing")]
    [SerializeField] private float rotateSpeed = 15f;

    [Header("VFX")]
    [Tooltip("타격 방향으로 발사되는 바람 이펙트")]
    [SerializeField] private GameObject blastVFXPrefab;
    [Tooltip("시전자 위치 기준 스폰 오프셋. Z는 조준 방향으로의 전방 거리, Y는 높이")]
    [SerializeField] private Vector3 blastVFXOffset = new Vector3(0f, 1f, 1f);
    [SerializeField] private float blastVFXLifetime = 2f;

    [Header("Cast VFX")]
    [Tooltip("이 액션을 선택해서 방향을 지정하는 동안 표시되는 이펙트. 영속 오브젝트(유닛)에 부모로 붙이지 않는다.")]
    [SerializeField] private Transform castVFXPrefab;
    [SerializeField] private Transform castVFXSpawnPoint;
    [SerializeField] private float castVFXHideDelay = 1.5f;

    public event EventHandler OnWindBlastStarted;
    public event EventHandler OnWindBlastEnded;

    private GridPosition targetGridPosition;
    private GameObject activeCastVFX;

    // 마우스 방향 기준 부채꼴 캐시 — 시각화와 클릭 판정이 동일한 리스트를 참조하도록
    private List<GridPosition> cachedConeList = new List<GridPosition>();
    private GridPosition cachedConeMouseGrid = new GridPosition(-999, -999, 0);

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }

    private void Start()
    {
        UnitActionSystem.Instance.OnSelectedActionChanged += OnSelectedActionChanged;
    }

    private void OnDestroy()
    {
        if (UnitActionSystem.Instance != null)
            UnitActionSystem.Instance.OnSelectedActionChanged -= OnSelectedActionChanged;
        HideCastVFX();
    }

    // ─── 시전 이펙트 (목표 지정 중 표시) ─────────────────────────────────

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

    public override string GetDescription()
    {
        int atk = unit.GetAttackPower();
        return $"전방 부채꼴 범위 내 적에게 {blastDamage + atk} 피해를 입히고 {pushDistance}칸 밀쳐낸다. 장애물 충돌 시 {collisionDamage + atk} 추가 피해.";
    }


    // ─── 부채꼴 계산 ───────────────────────────────────────────────────

    /// 시전자 위치에서 aimDir 방향으로 부채꼴 내 타일 목록 반환
    private List<GridPosition> GetConeTiles(GridPosition origin, Vector2 aimDir)
    {
        List<GridPosition> list = new List<GridPosition>();
        if (aimDir == Vector2.zero) return list;

        for (int x = -coneRange; x <= coneRange; x++)
        {
            for (int z = -coneRange; z <= coneRange; z++)
            {
                if (x == 0 && z == 0) continue;

                float dist = Mathf.Sqrt(x * x + z * z);
                if (dist > coneRange) continue;

                // 해당 타일 방향과 조준 방향 사이 각도 확인
                Vector2 toTile = new Vector2(x, z).normalized;
                float angle = Vector2.Angle(aimDir, toTile);
                if (angle > coneAngle) continue;

                GridPosition pos = new GridPosition(origin.x + x, origin.z + z, origin.floor);
                if (!LevelGrid.Instance.IsValidGridPosition(pos)) continue;
                if (!PathFinding.Instance.IsDirectlyTargetable(pos)) continue;

                // 장애물 시야 차단 체크 (ShootAction과 동일하게 유닛 콜라이더 높이 기준)
                if (obstacleLayerMask != 0)
                {
                    float height = unit.GetCollider().bounds.size.y * 0.8f;
                    Vector3 startPos = unit.GetWorldPosition() + Vector3.up * height;
                    Vector3 endPos = LevelGrid.Instance.GetWorldPosition(pos) + Vector3.up * height;
                    Vector3 dir = (endPos - startPos).normalized;
                    float distance = Vector3.Distance(startPos, endPos);
                    if (Physics.Raycast(startPos, dir, distance, obstacleLayerMask))
                        continue;
                }

                list.Add(pos);
            }
        }
        return list;
    }

    /// 그리드 위치 기준 조준 방향 벡터 (시전자 → 타겟)
    private Vector2 GetAimDir(GridPosition from, GridPosition to)
    {
        return new Vector2(to.x - from.x, to.z - from.z).normalized;
    }

    /// 마우스 월드 좌표 기준 현재 조준 방향
    private Vector2 GetMouseAimDir()
    {
        GridPosition unitPos = unit.GetGridPosition();
        Vector3 mouseWorld = MouseWorld.GetPosition();
        GridPosition mouseGrid = LevelGrid.Instance.GetGridPosition(mouseWorld);
        return GetAimDir(unitPos, mouseGrid);
    }

    /// 밀쳐내기 방향 (8방향 스냅)
    private Vector2Int GetPushDir(Vector2 aimDir)
    {
        int sx = aimDir.x == 0 ? 0 : (aimDir.x > 0 ? 1 : -1);
        int sz = aimDir.y == 0 ? 0 : (aimDir.y > 0 ? 1 : -1);
        float ax = Mathf.Abs(aimDir.x), az = Mathf.Abs(aimDir.y);
        if (ax >= az * 2f) return new Vector2Int(sx, 0);
        if (az >= ax * 2f) return new Vector2Int(0, sz);
        return new Vector2Int(sx, sz);
    }

    // ─── 유효 액션 위치 ────────────────────────────────────────────────

    /// 마우스 방향 기준 부채꼴 내 타일 반환 → 그리드 비주얼이 마우스 이동마다
    /// UpdateGridVisual()을 호출하므로 부채꼴이 자동으로 회전됨
    public override List<GridPosition> GetValidActionGridPositionList()
    {
        GridPosition unitPos = unit.GetGridPosition();
        GridPosition mouseGrid = LevelGrid.Instance.GetGridPosition(MouseWorld.GetPosition());

        // 마우스가 유닛 자신 위치거나 사거리 밖/장애물이면 빈 리스트 → 흰색 범위만 표시
        if (mouseGrid == unitPos
            || !LevelGrid.Instance.IsValidGridPosition(mouseGrid)
            || !PathFinding.Instance.IsDirectlyTargetable(mouseGrid))
        {
            cachedConeList = new List<GridPosition>();
            cachedConeMouseGrid = mouseGrid;
            return cachedConeList;
        }
        float dist = Mathf.Sqrt(
            (mouseGrid.x - unitPos.x) * (mouseGrid.x - unitPos.x) +
            (mouseGrid.z - unitPos.z) * (mouseGrid.z - unitPos.z));
        if (dist > coneRange)
        {
            cachedConeList = new List<GridPosition>();
            cachedConeMouseGrid = mouseGrid;
            return cachedConeList;
        }

        // 마우스 그리드가 바뀌었을 때만 재계산
        if (mouseGrid != cachedConeMouseGrid)
        {
            cachedConeList = GetConeTiles(unitPos, GetMouseAimDir());
            cachedConeMouseGrid = mouseGrid;
        }
        return cachedConeList;
    }

    public override bool IsValidActionGridPosition(GridPosition gridPosition)
    {
        // 부채꼴에 효과를 줄 수 있는 타일이 하나라도 있으면,
        // 사거리 내 어느 타일을 클릭해도 발동 허용
        if (cachedConeList.Count > 0)
        {
            GridPosition unitPos = unit.GetGridPosition();
            float dist = Mathf.Sqrt(
                (gridPosition.x - unitPos.x) * (gridPosition.x - unitPos.x) +
                (gridPosition.z - unitPos.z) * (gridPosition.z - unitPos.z));
            return dist <= coneRange && LevelGrid.Instance.IsValidGridPosition(gridPosition);
        }
        return false;
    }

    /// 흰색으로 표시할 전방향 원형 최대 사거리
    public override List<GridPosition> GetActionRangeGridPositionList()
    {
        GridPosition unitPos = unit.GetGridPosition();
        List<GridPosition> rangeList = new List<GridPosition>();

        for (int x = -coneRange; x <= coneRange; x++)
        {
            for (int z = -coneRange; z <= coneRange; z++)
            {
                if (x == 0 && z == 0) continue;
                if (Mathf.Sqrt(x * x + z * z) > coneRange) continue;

                GridPosition pos = new GridPosition(unitPos.x + x, unitPos.z + z, unitPos.floor);
                if (!LevelGrid.Instance.IsValidGridPosition(pos)) continue;
                if (!PathFinding.Instance.IsDirectlyTargetable(pos)) continue;

                rangeList.Add(pos);
            }
        }
        return rangeList;
    }

    /// 마우스 오버 시 빨간색으로 표시할 부채꼴 — 현재 녹색 타일과 동일한 캐시 반환
    public override List<GridPosition> GetDamageAffectedGridPosition(GridPosition targetPos)
    {
        return cachedConeList;
    }

    // ─── 액션 실행 ─────────────────────────────────────────────────────

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        targetGridPosition = gridPosition;
        HideCastVFXDelayed();
        ActionStart(onActionComplete);
        StartCoroutine(WindBlastRoutine());
    }

    private IEnumerator WindBlastRoutine()
    {
        // 타겟 방향으로 회전
        Vector3 aimWorldPos = LevelGrid.Instance.GetWorldPosition(targetGridPosition);
        aimWorldPos.y = transform.position.y;
        while (Vector3.Angle(transform.forward, (aimWorldPos - transform.position).normalized) > 1f)
        {
            Vector3 dir = (aimWorldPos - transform.position).normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * rotateSpeed);
            yield return null;
        }

        OnWindBlastStarted?.Invoke(this, EventArgs.Empty);
        SpawnBlastVFX();

        Vector2 aimDir = GetAimDir(unit.GetGridPosition(), targetGridPosition);
        Vector2Int pushDir = GetPushDir(aimDir);

        // 부채꼴 내 적 수집
        List<Unit> targets = new List<Unit>();
        foreach (GridPosition pos in GetConeTiles(unit.GetGridPosition(), aimDir))
        {
            if (!LevelGrid.Instance.IsGridPositionOccupied(pos)) continue;
            Unit target = LevelGrid.Instance.GetUnitListAtGridPosition(pos)[0];
            if (TeamHelper.IsHostile(unit.GetTeamType(), target.GetTeamType()) && !target.IsStealthed())
                targets.Add(target);
        }

        // 밀쳐내기 (피해는 PushUnitRoutine 착지 후 적용)
        List<Coroutine> pushRoutines = new List<Coroutine>();
        foreach (Unit target in targets)
            pushRoutines.Add(StartCoroutine(PushUnitRoutine(target, pushDir)));
        foreach (Coroutine c in pushRoutines)
            yield return c;

        OnWindBlastEnded?.Invoke(this, EventArgs.Empty);
        ActionComplete();
    }

    /// <summary>회전이 끝나 조준 방향이 확정된 시점(transform.forward 기준)에 전방으로 이펙트를 스폰한다.</summary>
    private void SpawnBlastVFX()
    {
        if (blastVFXPrefab == null) return;

        Vector3 spawnPos = unit.GetWorldPosition()
                          + Vector3.up * blastVFXOffset.y
                          + transform.forward * blastVFXOffset.z
                          + transform.right * blastVFXOffset.x;

        GameObject vfx = Instantiate(blastVFXPrefab, spawnPos, transform.rotation);
        Destroy(vfx, blastVFXLifetime);
    }

    private IEnumerator PushUnitRoutine(Unit target, Vector2Int pushDir)
    {
        float cellSize = LevelGrid.Instance.GetCellSize();
        GridPosition startPos = target.GetGridPosition();
        Vector3 startWorldPos = target.transform.position;

        // 착지 위치 및 충돌 계산
        GridPosition landPos = startPos;
        bool collided = false;
        Unit collidedUnit = null;

        for (int step = 1; step <= pushDistance; step++)
        {
            GridPosition nextPos = new GridPosition(
                startPos.x + pushDir.x * step,
                startPos.z + pushDir.y * step,
                startPos.floor
            );

            if (!LevelGrid.Instance.IsValidGridPosition(nextPos) ||
                !PathFinding.Instance.IsDirectlyTargetable(nextPos))
            {
                collided = true;
                break;
            }

            if (LevelGrid.Instance.IsGridPositionOccupied(nextPos))
            {
                collidedUnit = LevelGrid.Instance.GetUnitListAtGridPosition(nextPos)[0];
                collided = true;
                break;
            }

            landPos = nextPos;
        }

        // HitReaction 설정 (사망 시 래그돌 방향)
        Vector3 blastDir = new Vector3(pushDir.x, 0.3f, pushDir.y).normalized;
        target.GetHitReaction().SetHitDirection(blastDir);
        target.GetHitReaction().SetHitForce(hitForce);

        int finalBlast = unit.CalculateDamage(blastDamage);
        int finalCollision = unit.CalculateDamage(collisionDamage);

        // 치사량이면 넉백 없이 즉시 피해 → 래그돌이 히트 리액션 방향으로 날아감
        int totalDamage = finalBlast + (collided ? finalCollision : 0);
        BarrierSystem barrierSystem = target.GetComponent<BarrierSystem>();
        int effectiveHP = (int)target.GetCurrentHealth()
                        + (barrierSystem != null ? barrierSystem.GetTotalBarrierAmount() : 0);
        if (effectiveHP <= totalDamage)
        {
            target.Damage(finalBlast);
            yield break;
        }

        // 착지 월드 좌표 계산
        float centerOffset = (target.GetSize() - 1) * cellSize * 0.5f;
        Vector3 offsetVec = new Vector3(centerOffset, 0f, centerOffset);
        Vector3 landWorldPos = LevelGrid.Instance.GetWorldPosition(landPos) + offsetVec;

        LayerMask snapMask = target.GetGroundSnapLayerMask();
        if (snapMask != 0 &&
            Physics.Raycast(landWorldPos + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 3f, snapMask))
        {
            landWorldPos.y = groundHit.point.y;
        }

        // 포물선 아크
        float elapsed = 0f;
        int movedSteps = Mathf.Max(Mathf.Abs(landPos.x - startPos.x), Mathf.Abs(landPos.z - startPos.z));
        float duration = Mathf.Max(pushDuration * movedSteps / pushDistance, 0.1f);

        Quaternion flyRotation = Quaternion.LookRotation(new Vector3(pushDir.x, 0f, pushDir.y));

        while (elapsed < duration)
        {
            if (target == null) yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float arc = Mathf.Sin(Mathf.PI * t) * arcHeight;
            Vector3 flatPos = Vector3.Lerp(startWorldPos, landWorldPos, t);
            flatPos.y += arc;

            target.transform.position = flatPos;
            target.transform.rotation = Quaternion.Lerp(target.transform.rotation, flyRotation, t * 5f);
            yield return null;
        }

        if (target == null) yield break;

        target.transform.position = landWorldPos;
        target.SetGridPosition(landPos);
        target.transform.rotation = flyRotation;

        // 착지 후 피해 적용 (finalBlast / finalCollision은 위에서 계산됨)
        target.Damage(finalBlast);

        if (target == null) yield break;

        if (collided)
        {
            target.Damage(finalCollision);
            if (collidedUnit != null) collidedUnit.Damage(finalCollision);
        }
    }

    // ─── AI ────────────────────────────────────────────────────────────

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        Vector2 aimDir = GetAimDir(unit.GetGridPosition(), gridPosition);
        int hitCount = 0;
        foreach (GridPosition pos in GetConeTiles(unit.GetGridPosition(), aimDir))
        {
            if (!LevelGrid.Instance.IsGridPositionOccupied(pos)) continue;
            Unit target = LevelGrid.Instance.GetUnitListAtGridPosition(pos)[0];
            if (TeamHelper.IsHostile(unit.GetTeamType(), target.GetTeamType()))
                hitCount++;
        }
        return hitCount > 0
            ? new EnemyAIAction { gridPosition = gridPosition, actionValue = 40 + hitCount * 15 }
            : null;
    }
}
