using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShootAction : BaseAction
{
    protected override string DefaultActionName() => "사격";
    private enum State
    {
        Aiming,
        Shooting,
        Cooloff
    }

    public class OnShootEventArgs : EventArgs
    {
        public Unit targetUnit;
        public Unit shootingUnit;
    }

    [SerializeField] private int maxShootDistance = 7;
    [SerializeField] private int shootDamage = 30;
    [SerializeField] private Transform bulletProjectilePrefab;
    [SerializeField] private Transform shootPointTransform;
    [SerializeField] private LayerMask obstacleLayerMask;

    private State state;
    private bool canShootBullet;
    private Unit targetUnit;
    private float rotateSpeed = 10f;

    // 유효 타겟 목록 캐시 — 매 UpdateGridVisual마다 레이캐스트를 다시 쏘는 비용을 줄임
    private List<GridPosition> cachedValidGridPositionList;
    private bool isCacheDirty = true;

    public event EventHandler<OnShootEventArgs> OnStartShooting;
    public static event EventHandler<OnShootEventArgs> OnAnyShooting;
    public event EventHandler OnStopShooting;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 2;
    }

    private void Start()
    {
        // 턴이 바뀌면 적 위치가 달라질 수 있으므로 캐시 무효화
        TurnSystem.Instance.OnTurnChanged += OnCacheInvalidated;
        // 이동·공격 등 액션이 끝나면 적이 죽거나 이동했을 수 있으므로 캐시 무효화
        BaseAction.OnAnyActionEnded += OnCacheInvalidated;
    }

    private void OnDestroy()
    {
        TurnSystem.Instance.OnTurnChanged -= OnCacheInvalidated;
        BaseAction.OnAnyActionEnded -= OnCacheInvalidated;
    }

    private void OnCacheInvalidated(object sender, EventArgs e) => isCacheDirty = true;

    private IEnumerator StateCheck()
    {
        while (state == State.Aiming)
        {
            AimToTarget();
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        if (canShootBullet && state == State.Shooting)
        {
            Shoot();
            canShootBullet = false;
        }

        yield return new WaitForSeconds(0.3f);
        state = State.Cooloff;

        yield return new WaitForSeconds(0.2f);
        ActionComplete();
    }    public override string GetDescription() =>
        $"사거리 {maxShootDistance} 내 적에게 {shootDamage + unit.GetAttackPower()} 피해를 입힌다.";


    public override List<GridPosition> GetValidActionGridPositionList()
    {
        // [최적화 전] 매 호출마다 전체 범위를 순회하며 레이캐스트
        // return GetValidActionGridPositionList(unit.GetGridPosition());

        // [최적화 후] 캐시가 유효하면 재계산 없이 바로 반환
        if (!isCacheDirty && cachedValidGridPositionList != null) return cachedValidGridPositionList;

        cachedValidGridPositionList = GetValidActionGridPositionList(unit.GetGridPosition());
        isCacheDirty = false;
        return cachedValidGridPositionList;
    }

    public List<GridPosition> GetValidActionGridPositionList(GridPosition unitGridPosition)
    {
        List<GridPosition> validGridPositionList = new List<GridPosition>();

        int floorAmount = LevelGrid.Instance.GetFloorAmount();
        int minFloor = Mathf.Clamp(unitGridPosition.floor - maxShootDistance, 0, floorAmount - 1);
        int maxFloor = Mathf.Clamp(unitGridPosition.floor + maxShootDistance, 0, floorAmount - 1);

        for (int x = -maxShootDistance; x <= maxShootDistance; x++)
        {
            for (int z = -maxShootDistance; z <= maxShootDistance; z++)
            {
                // 유닛 사이즈에 따른 오프셋 계산 (2x2 유닛 대응용)
                float sizeOffset = (unit.GetSize() - 1) * 0.5f;
                float distanceX = x - sizeOffset;
                float distanceZ = z - sizeOffset;

                if (Mathf.Sqrt(distanceX * distanceX + distanceZ * distanceZ) > maxShootDistance) continue;

                for (int floor = minFloor; floor <= maxFloor; floor++)
                {
                    GridPosition testGridPosition = new GridPosition(unitGridPosition.x + x, unitGridPosition.z + z, floor);

                    if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;
                    if (!LevelGrid.Instance.IsGridPositionOccupied(testGridPosition)) continue;

                    foreach (Unit targetUnit in LevelGrid.Instance.GetUnitListAtGridPosition(testGridPosition))
                    {
                        if (!TeamHelper.IsHostile(unit.GetTeamType(), targetUnit.GetTeamType())) continue;
                        if (targetUnit.IsStealthed()) continue; // 은신 유닛 제외

                        if (IsTargetVisible(targetUnit))
                        {
                            validGridPositionList.Add(testGridPosition);
                        }
                    }
                }
            }
        }
        return validGridPositionList;
    }

    private bool IsTargetVisible(Unit target)
    {
        Vector3 startPos;
        if (shootPointTransform != null)
        {
            startPos = shootPointTransform.position;
        }
        else
        {
            // ShootPoint가 없을 경우 유닛의 키 높이(80%)에서 레이 쏨
            startPos = unit.GetWorldPosition() + Vector3.up * (unit.GetCollider().bounds.size.y * 0.8f);
        }

        Vector3 targetPos = target.GetWorldPosition() + Vector3.up * (target.GetCollider().bounds.size.y * 0.8f);
        Vector3 dirToTarget = (targetPos - startPos).normalized;
        float distance = Vector3.Distance(startPos, targetPos);

        // 장애물 레이캐스트 확인
        return !Physics.Raycast(startPos, dirToTarget, distance, obstacleLayerMask);
    }

    public override List<GridPosition> GetActionRangeGridPositionList()
    {
        List<GridPosition> rangeList = new List<GridPosition>();
        GridPosition unitGridPosition = unit.GetGridPosition();

        int floorAmount = LevelGrid.Instance.GetFloorAmount();
        int minFloor = Mathf.Clamp(unitGridPosition.floor - maxShootDistance, 0, floorAmount - 1);
        int maxFloor = Mathf.Clamp(unitGridPosition.floor + maxShootDistance, 0, floorAmount - 1);

        for (int x = -maxShootDistance; x <= maxShootDistance; x++)
        {
            for (int z = -maxShootDistance; z <= maxShootDistance; z++)
            {
                float sizeOffset = (unit.GetSize() - 1) * 0.5f;
                float distanceX = x - sizeOffset;
                float distanceZ = z - sizeOffset;

                if (Mathf.Sqrt(distanceX * distanceX + distanceZ * distanceZ) > maxShootDistance) continue;

                for (int floor = minFloor; floor <= maxFloor; floor++)
                {
                    GridPosition testGridPosition = new GridPosition(unitGridPosition.x + x, unitGridPosition.z + z, floor);
                    // 바닥 없는 허공 타일 제외 + 계단 exclusive 타일 포함 (적이 서 있을 수 있으므로)
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

    public override void TakeAction(GridPosition gridPosition, Action onShootComplete)
    {
        List<Unit> targetUnitList = LevelGrid.Instance.GetUnitListAtGridPosition(gridPosition);
        if (targetUnitList.Count == 0)
        {
            Debug.LogError("공격하려는데 타겟 없음: " + gridPosition.ToString());
            return;
        }

        targetUnit = targetUnitList[0];
        canShootBullet = true;
        state = State.Aiming;
        ActionStart(onShootComplete);
        StartCoroutine(StateCheck());
    }

    private void Shoot()
    {
        OnStartShooting?.Invoke(this, new OnShootEventArgs { targetUnit = targetUnit, shootingUnit = unit });
        OnAnyShooting?.Invoke(this, new OnShootEventArgs { targetUnit = targetUnit, shootingUnit = unit });

        Transform bulletTransform = Instantiate(bulletProjectilePrefab, shootPointTransform.position, Quaternion.identity);
        BulletProjectile bulletProjectile = bulletTransform.GetComponent<BulletProjectile>();
        bulletProjectile.OnBulletHit += BulletProjectile_OnBulletHit;

        // 타겟의 중심부(키의 80%)를 조준
        Vector3 targetHitPos = targetUnit.GetWorldPosition() + Vector3.up * (targetUnit.GetCollider().bounds.size.y * 0.8f);
        bulletProjectile.Setup(targetHitPos);
    }

    private void AimToTarget()
    {
        Vector3 targetPos = targetUnit.transform.position;
        targetPos.y = transform.position.y; // 높이 차이 무시하고 회전
        Vector3 moveDir = (targetPos - transform.position).normalized;

        transform.forward = Vector3.Slerp(transform.forward, moveDir, Time.deltaTime * rotateSpeed);

        if (Vector3.Angle(transform.forward, moveDir) < 1f)
        {
            state = State.Shooting;
        }
    }

    private void BulletProjectile_OnBulletHit(object sender, BulletProjectile.OnBulletHitEventArgs e)
    {
        Vector3 hitDir = (e.hitPosition - transform.position).normalized;
        targetUnit.GetHitReaction().SetHitDirection(hitDir);
        targetUnit.GetHitReaction().SetHitForce(e.hitForce);
        targetUnit.Damage(unit.CalculateDamage(shootDamage));
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        Unit targetUnit = LevelGrid.Instance.GetUnitListAtGridPosition(gridPosition)[0];

        // AI 우선순위 계산 (적 체력이 낮을수록, 팀에 따라 가치 부여)
        int actionValue = (targetUnit.GetTeamType() != TeamType.Player) ?
            500 + (100 - Mathf.RoundToInt(targetUnit.GetHealthNormalized() * 100f)) :
            1000 + (100 - Mathf.RoundToInt(targetUnit.GetHealthNormalized() * 100f));

        return new EnemyAIAction { gridPosition = gridPosition, actionValue = actionValue };
    }

    public int GetTargetCountAtPosition(GridPosition gridPosition) => GetValidActionGridPositionList(gridPosition).Count;
    public Unit GetTargetUnit() => targetUnit;
    public int GetMaxShootDistance() => maxShootDistance;
}