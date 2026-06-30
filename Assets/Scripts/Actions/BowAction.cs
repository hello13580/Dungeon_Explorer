using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BowAction : BaseAction
{
    protected override string DefaultActionName() => "활 공격";
    public override ActionCategory GetActionCategory() => ActionCategory.Attack;
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
    [SerializeField] private int shootDamage = 10;
    [SerializeField] private Transform ArrowProjectilePrefab;
    [SerializeField] private Transform shootPointTransform;
    [SerializeField] private LayerMask obstacleLayerMask;
    [SerializeField] private Transform ArrowInBow;

    private State state;
    private Unit targetUnit;
    private float rotateSpeed = 10f;
    private bool arrowShot = false;
    // BowAction과 PiercingArrowAction이 같은 활쏘기 애니메이션·이벤트를 공유하므로,
    // 실제로 이 액션이 발사 대기 중일 때만 ShootArrow() 호출에 반응하도록 구분한다.
    private bool awaitingArrowRelease = false;

    // 유효 타겟 목록 캐시 — 매 UpdateGridVisual마다 레이캐스트를 다시 쏘는 비용을 줄임
    private List<GridPosition> cachedValidGridPositionList;
    private bool isCacheDirty = true;

    // 연발 사격(RapidFireAction)으로 부여된, 액션 포인트 1 할인이 적용될 남은 발사 횟수
    private int discountedShotsRemaining = 0;

    /// <summary>다음 count번의 활 공격에 액션 포인트 1 할인을 적용한다.</summary>
    public void AddDiscountedShots(int count) => discountedShotsRemaining += count;

    /// <summary>할인이 남아있으면 1 적게, 없으면 평소대로.</summary>
    public override int GetActionPointCost() =>
        discountedShotsRemaining > 0 ? Mathf.Max(0, actionCost - 1) : actionCost;

    public event EventHandler<OnShootEventArgs> OnStartDrawing;
    public static event EventHandler<OnShootEventArgs> OnAnyShooting;
    public event EventHandler OnStopShooting;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
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

        arrowShot = false;
        awaitingArrowRelease = true;
        OnStartDrawing?.Invoke(this, new OnShootEventArgs { targetUnit = targetUnit, shootingUnit = unit });
        ArrowInBow.gameObject.SetActive(true);
        OnAnyShooting?.Invoke(this, new OnShootEventArgs { targetUnit = targetUnit, shootingUnit = unit });

        // Animation Event(OnBowRelease)가 ShootArrow()를 호출할 때까지 대기
        yield return new WaitUntil(() => arrowShot);
        ArrowInBow.gameObject.SetActive(false);

        yield return new WaitForSeconds(0.3f);
        state = State.Cooloff;
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
                // ���� ����� ���� ������ ��� (2x2 ���� ������)
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
                        if (targetUnit.IsStealthed()) continue; // ���� ���� ����

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
            // ShootPoint�� ���� ��� ������ Ű ����(80%)���� ���� ��
            startPos = unit.GetWorldPosition() + Vector3.up * (unit.GetCollider().bounds.size.y * 0.8f);
        }

        Vector3 targetPos = target.GetWorldPosition() + Vector3.up * (target.GetCollider().bounds.size.y * 0.8f);
        Vector3 dirToTarget = (targetPos - startPos).normalized;
        float distance = Vector3.Distance(startPos, targetPos);

        // ��ֹ� ����ĳ��Ʈ Ȯ��
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
            Debug.LogError("�����Ϸ��µ� Ÿ�� ����: " + gridPosition.ToString());
            return;
        }

        // 액션 포인트는 이미 GetActionPointCost()의 할인된 값으로 소모된 뒤이므로,
        // 실제로 발사가 확정된 이 시점에 할인 횟수를 차감한다.
        if (discountedShotsRemaining > 0)
            discountedShotsRemaining--;

        targetUnit = targetUnitList[0];
        state = State.Aiming;
        ActionStart(onShootComplete);
        StartCoroutine(StateCheck());
    }

    public void ShootArrow()
    {
        // 다른 액션(PiercingArrowAction 등)이 같은 애니메이션 이벤트를 공유해서 호출한 경우 무시
        if (!awaitingArrowRelease) return;
        awaitingArrowRelease = false;

        if (targetUnit == null) return;

        Transform arrowTransform = Instantiate(ArrowProjectilePrefab, shootPointTransform.position, Quaternion.identity);
        ArrowProjectile arrowProjectile = arrowTransform.GetComponent<ArrowProjectile>();
        arrowProjectile.OnArrowHit += ArrowProjectile_OnArrowHit;

        Vector3 targetHitPos = targetUnit.GetWorldPosition() + Vector3.up * (targetUnit.GetCollider().bounds.size.y * 0.8f);
        arrowProjectile.Setup(targetHitPos);

        arrowShot = true;
    }

    private void AimToTarget()
    {
        Vector3 targetPos = targetUnit.transform.position;
        targetPos.y = transform.position.y; // ���� ���� �����ϰ� ȸ��
        Vector3 moveDir = (targetPos - transform.position).normalized;

        transform.forward = Vector3.Slerp(transform.forward, moveDir, Time.deltaTime * rotateSpeed);

        if (Vector3.Angle(transform.forward, moveDir) < 1f)
        {
            state = State.Shooting;
        }
    }

    private void ArrowProjectile_OnArrowHit(object sender, ArrowProjectile.OnBulletHitEventArgs e)
    {
        Vector3 hitDir = (e.hitPosition - transform.position).normalized;
        targetUnit.GetHitReaction().SetHitDirection(hitDir);
        targetUnit.GetHitReaction().SetHitForce(e.hitForce);

        // 브로드헤드 화살촉이 장착되어 있으면 피해 계산 전에 배율을 소진해 반영한다
        ArrowEffectAction arrowEffect = unit.GetAction<ArrowEffectAction>();
        float damageMultiplier = arrowEffect != null ? arrowEffect.ConsumeDamageMultiplier() : 1f;
        int finalDamage = Mathf.RoundToInt(unit.CalculateDamage(shootDamage) * damageMultiplier);
        targetUnit.Damage(finalDamage);

        // 장착된 나머지 효과(취약/약화)가 있으면 적용하고 소진한다
        if (arrowEffect != null && arrowEffect.HasPendingEffect())
            arrowEffect.ApplyEffectToTarget(targetUnit);
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        Unit targetUnit = LevelGrid.Instance.GetUnitListAtGridPosition(gridPosition)[0];

        // 현재 체력 절대값이 낮을수록 높은 우선순위 (체력이 낮은 적을 집중 공략)
        int currentHealth = targetUnit.GetCurrentHealth();
        int actionValue = 1000 + (500 - currentHealth);

        return new EnemyAIAction { gridPosition = gridPosition, actionValue = actionValue };
    }

    public int GetTargetCountAtPosition(GridPosition gridPosition) => GetValidActionGridPositionList(gridPosition).Count;
    public Unit GetTargetUnit() => targetUnit;
    public int GetMaxShootDistance() => maxShootDistance;
}
