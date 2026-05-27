using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AOEAction : BaseAction
{
    private enum State { Aiming, Throwing }

    public event EventHandler OnAOEActionStarted;
    [SerializeField] private Transform grenadeProjectilePrefab;
    [SerializeField] private Transform shootPointTransform;
    [SerializeField] private LayerMask obstacleLayerMask;

    [SerializeField] private int maxRange = 7;
    [SerializeField] private int damageRadius = 1;
    [SerializeField] private float targetingYAxis = 0.5f;
    [SerializeField] private float rotateSpeed = 15f;
    private GridPosition targetGridPosition;
    private State state;

    // 유효 투척 위치 목록 캐시 — 매 UpdateGridVisual마다 레이캐스트를 다시 쏘는 비용을 줄임
    private List<GridPosition> cachedValidGridPositionList;
    private bool isCacheDirty = true;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }

    private void Start()
    {
        // 턴이 바뀌면 장애물 상태가 달라질 수 있으므로 캐시 무효화
        TurnSystem.Instance.OnTurnChanged += OnCacheInvalidated;
        // 이동·공격 등 액션이 끝나면 유닛이나 오브젝트 위치가 바뀔 수 있으므로 캐시 무효화
        BaseAction.OnAnyActionEnded += OnCacheInvalidated;
    }

    private void OnDestroy()
    {
        TurnSystem.Instance.OnTurnChanged -= OnCacheInvalidated;
        BaseAction.OnAnyActionEnded -= OnCacheInvalidated;
    }

    private void OnCacheInvalidated(object sender, EventArgs e) => isCacheDirty = true;

    public override string GetActionName() => "Grenade";

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        ActionStart(onActionComplete);
        targetGridPosition = gridPosition;
        state = State.Aiming;
        StartCoroutine(AimAndThrowRoutine());
    }

    private IEnumerator AimAndThrowRoutine()
    {
        while (state == State.Aiming)
        {
            AimToTarget();
            yield return null;
        }

        OnAOEActionStarted?.Invoke(this, EventArgs.Empty);
    }

    private void AimToTarget()
    {
        Vector3 targetPos = LevelGrid.Instance.GetWorldPosition(targetGridPosition);
        targetPos.y = transform.position.y;

        Vector3 aimDir = (targetPos - transform.position).normalized;
        transform.forward = Vector3.Slerp(transform.forward, aimDir, Time.deltaTime * rotateSpeed);

        if (Vector3.Angle(transform.forward, aimDir) < 1f)
        {
            state = State.Throwing;
        }
    }

    public void ThrowGrenade()
    {
        // ����ź ���� �� ����
        Transform grenadeTransform = Instantiate(grenadeProjectilePrefab, shootPointTransform.position, Quaternion.identity);
        GrenadeProjectile grenadeProjectile = grenadeTransform.GetComponent<GrenadeProjectile>();

        // ����ź ��ô �� �Ϸ� �� ActionComplete ȣ��ǵ��� ����
        grenadeProjectile.Setup(targetGridPosition, damageRadius, this, ActionComplete);
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
                // ���� ���� üũ
                if (Mathf.Sqrt(x * x + z * z) > maxRange) continue;

                for (int floor = minFloor; floor <= maxFloor; floor++)
                {
                    GridPosition testGridPosition = new GridPosition(unitGridPosition.x + x, unitGridPosition.z + z, floor);

                    if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;
                    if (!PathFinding.Instance.IsWalkableGridPosition(testGridPosition)) continue;

                    rangeList.Add(testGridPosition);
                }
            }
        }
        return rangeList;
    }

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
        List<GridPosition> validList = new List<GridPosition>();

        int floorAmount = LevelGrid.Instance.GetFloorAmount();
        int minFloor = Mathf.Clamp(unitGridPosition.floor - maxRange, 0, floorAmount - 1);
        int maxFloor = Mathf.Clamp(unitGridPosition.floor + maxRange, 0, floorAmount - 1);

        for (int x = -maxRange; x <= maxRange; x++)
        {
            for (int z = -maxRange; z <= maxRange; z++)
            {
                if (Mathf.Sqrt(x * x + z * z) > maxRange) continue;

                for (int floor = minFloor; floor <= maxFloor; floor++)
                {
                    GridPosition testGridPosition = new GridPosition(unitGridPosition.x + x, unitGridPosition.z + z, floor);

                    if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;
                    if (!PathFinding.Instance.IsWalkableGridPosition(testGridPosition)) continue;

                    // ��ô ���� ���� (��ֹ� üũ)
                    Vector3 startPos;
                    if (shootPointTransform != null)
                    {
                        startPos = shootPointTransform.position;
                    }
                    else
                    {
                        startPos = unit.GetWorldPosition() + Vector3.up * (unit.GetCollider().bounds.size.y * 0.75f);
                    }

                    Vector3 targetPos = LevelGrid.Instance.GetWorldPosition(testGridPosition) + Vector3.up * targetingYAxis;
                    Vector3 dirToTarget = (targetPos - startPos).normalized;
                    float distance = Vector3.Distance(startPos, targetPos);

                    // ����ĳ��Ʈ�� ��ô ��ο� ���� �ִ��� Ȯ��
                    if (!Physics.Raycast(startPos, dirToTarget, distance, obstacleLayerMask))
                    {
                        validList.Add(testGridPosition);
                    }
                }
            }
        }
        return validList;
    }

    public override List<GridPosition> GetDamageAffectedGridPosition(GridPosition targetGridPosition)
    {
        return GetDamageAffectedGridPosition(targetGridPosition, damageRadius);
    }

    public List<GridPosition> GetDamageAffectedGridPosition(GridPosition targetGridPosition, int radius)
    {
        List<GridPosition> affectedList = new List<GridPosition>();

        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                GridPosition testGridPosition = targetGridPosition + new GridPosition(x, z, 0);

                if (!LevelGrid.Instance.IsValidGridPosition(testGridPosition)) continue;

                // �߽����� ������ ����, �� �ܴ� ��ֹ� ���� Ȯ��
                if (targetGridPosition == testGridPosition || HasClearLineOfSight(targetGridPosition, testGridPosition))
                {
                    affectedList.Add(testGridPosition);
                }
            }
        }
        return affectedList;
    }

    private bool HasClearLineOfSight(GridPosition centerGridPosition, GridPosition targetGridPosition)
    {
        Vector3 centerPos = LevelGrid.Instance.GetWorldPosition(centerGridPosition) + Vector3.up * 1f;
        Vector3 targetPos = LevelGrid.Instance.GetWorldPosition(targetGridPosition) + Vector3.up * 1f;

        Vector3 dir = (targetPos - centerPos).normalized;
        float distance = Vector3.Distance(centerPos, targetPos);

        // ���� �߽������� Ÿ�� �׸��� ���̿� ���� �ִ��� üũ
        return !Physics.Raycast(centerPos, dir, distance, obstacleLayerMask);
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        // AI�� ����ź�� ���� ���� ��ġ �Ǵ� (���� -40���� �Ǿ� ������ ���� �߰� ����)
        return new EnemyAIAction
        {
            gridPosition = gridPosition,
            actionValue = -40
        };
    }
}