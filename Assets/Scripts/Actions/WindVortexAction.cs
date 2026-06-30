using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지정한 위치를 중심으로 소용돌이를 일으켜 범위 내 모든 적을 바깥으로 밀쳐내는 스킬.
/// WindBlastAction과 같은 밀쳐내기 로직(포물선 이동, 충돌 처리)을 쓰지만,
/// 부채꼴이 아니라 중심점 기준 사방으로 미는 원형 범위라는 점이 다르다.
/// 애니메이션 없이 이펙트만 재생하고, 끝나면 2초 딜레이 후 액션이 종료된다.
/// </summary>
public class WindVortexAction : BaseAction
{
    protected override string DefaultActionName() => "바람 소용돌이";
    public override ActionCategory GetActionCategory() => ActionCategory.Attack;

    [Header("Range")]
    [SerializeField] private int maxRange = 6;
    [SerializeField] private int vortexRadius = 2; // 효과 반경 (칸)

    [Header("각도")]
    [Tooltip("시전자 → 중심점 방향 기준 부채꼴 반각(도). 180이면 제한 없이 원형 그대로, 작을수록 좁은 부채꼴이 된다.")]
    [Range(1f, 180f)]
    [SerializeField] private float coneAngle = 180f;

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

    [Header("이펙트")]
    [Tooltip("중심점에 스폰되는 소용돌이 이펙트")]
    [SerializeField] private GameObject vortexVFXPrefab;
    [SerializeField] private Vector3 vortexVFXOffset = Vector3.zero;
    [SerializeField] private float vortexVFXLifetime = 2f;

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
        return $"지정한 위치 반경 {vortexRadius}칸 내 모든 적에게 {blastDamage + atk} 피해를 입히고 {pushDistance}칸 밀쳐낸다. " +
               $"장애물 충돌 시 {collisionDamage + atk} 추가 피해.";
    }

    // ─── 범위 타일 ────────────────────────────────────────────────────

    private List<GridPosition> GetVortexArea(GridPosition center)
    {
        List<GridPosition> list = new List<GridPosition>();

        // 시전자 → 중심점 방향을 부채꼴의 기준 축으로 삼는다.
        // coneAngle이 180이면 어떤 각도든 통과하므로 사실상 원형 그대로 동작한다.
        GridPosition unitPos = unit.GetGridPosition();
        Vector2 aimDir = new Vector2(center.x - unitPos.x, center.z - unitPos.z);
        bool useCone = coneAngle < 179.9f && aimDir != Vector2.zero;
        if (useCone) aimDir.Normalize();

        for (int x = -vortexRadius; x <= vortexRadius; x++)
        {
            for (int z = -vortexRadius; z <= vortexRadius; z++)
            {
                if (Mathf.Sqrt(x * x + z * z) > vortexRadius) continue;

                if (useCone && (x != 0 || z != 0))
                {
                    Vector2 toTile = new Vector2(x, z).normalized;
                    if (Vector2.Angle(aimDir, toTile) > coneAngle) continue;
                }

                GridPosition pos = new GridPosition(center.x + x, center.z + z, center.floor);
                if (LevelGrid.Instance.IsValidGridPosition(pos))
                    list.Add(pos);
            }
        }
        return list;
    }

    /// 중심점 → 대상 방향을 8방향으로 스냅한 밀쳐내기 방향
    private Vector2Int GetRadialPushDir(GridPosition center, GridPosition target)
    {
        Vector2 dir = new Vector2(target.x - center.x, target.z - center.z);
        if (dir == Vector2.zero) dir = Vector2.up; // 정확히 중심에 있으면 임의 방향

        int sx = dir.x == 0 ? 0 : (dir.x > 0 ? 1 : -1);
        int sz = dir.y == 0 ? 0 : (dir.y > 0 ? 1 : -1);
        float ax = Mathf.Abs(dir.x), az = Mathf.Abs(dir.y);
        if (ax >= az * 2f) return new Vector2Int(sx, 0);
        if (az >= ax * 2f) return new Vector2Int(0, sz);
        return new Vector2Int(sx, sz);
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
                        Vector3 targetPos = LevelGrid.Instance.GetWorldPosition(testPos) + Vector3.up * 0.5f;
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
        return GetVortexArea(targetGridPosition);
    }

    // ─── 액션 실행 ─────────────────────────────────────────────────────

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        usedThisTurn = true;
        ActionStart(onActionComplete);
        StartCoroutine(VortexRoutine(gridPosition));
    }

    private IEnumerator VortexRoutine(GridPosition center)
    {
        SpawnVortexVFX(center);

        // 범위 내 적 수집
        List<Unit> targets = new List<Unit>();
        foreach (GridPosition pos in GetVortexArea(center))
        {
            if (!LevelGrid.Instance.IsGridPositionOccupied(pos)) continue;
            Unit target = LevelGrid.Instance.GetUnitListAtGridPosition(pos)[0];
            if (TeamHelper.IsHostile(unit.GetTeamType(), target.GetTeamType()) && !target.IsStealthed())
                targets.Add(target);
        }

        // 중심에서 사방으로 밀쳐내기 (피해는 PushUnitRoutine 착지 후 적용)
        List<Coroutine> pushRoutines = new List<Coroutine>();
        foreach (Unit target in targets)
        {
            Vector2Int pushDir = GetRadialPushDir(center, target.GetGridPosition());
            pushRoutines.Add(StartCoroutine(PushUnitRoutine(target, pushDir)));
        }
        foreach (Coroutine c in pushRoutines)
            yield return c;

        // 애니메이션 없이 결과 적용 후 2초 딜레이
        yield return new WaitForSeconds(2f);

        ActionComplete();
    }

    private void SpawnVortexVFX(GridPosition center)
    {
        if (vortexVFXPrefab == null) return;

        Vector3 spawnPos = LevelGrid.Instance.GetWorldPosition(center) + vortexVFXOffset;
        GameObject vfx = Instantiate(vortexVFXPrefab, spawnPos, Quaternion.identity);
        Destroy(vfx, vortexVFXLifetime);
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

        // 착지 후 피해 적용
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
        int hitCount = 0;
        foreach (GridPosition pos in GetVortexArea(gridPosition))
        {
            if (!LevelGrid.Instance.IsGridPositionOccupied(pos)) continue;
            Unit target = LevelGrid.Instance.GetUnitListAtGridPosition(pos)[0];
            if (TeamHelper.IsHostile(unit.GetTeamType(), target.GetTeamType()))
                hitCount++;
        }
        return hitCount > 0
            ? new EnemyAIAction { gridPosition = gridPosition, actionValue = 40 + hitCount * 20 }
            : null;
    }
}
