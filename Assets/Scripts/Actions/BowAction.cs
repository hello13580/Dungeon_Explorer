using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BowAction : BaseAction
{
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

    public event EventHandler<OnShootEventArgs> OnStartDrawing;
    public static event EventHandler<OnShootEventArgs> OnAnyShooting;
    public event EventHandler OnStopShooting;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 2;
    }

    private IEnumerator StateCheck()
    {
        while (state == State.Aiming)
        {
            AimToTarget();
            yield return null;
        }

        arrowShot = false;
        OnStartDrawing?.Invoke(this, new OnShootEventArgs { targetUnit = targetUnit, shootingUnit = unit });
        ArrowInBow.gameObject.SetActive(true);
        OnAnyShooting?.Invoke(this, new OnShootEventArgs { targetUnit = targetUnit, shootingUnit = unit });

        // Animation Event(OnBowRelease)가 ShootArrow()를 호출할 때까지 대기
        yield return new WaitUntil(() => arrowShot);
        ArrowInBow.gameObject.SetActive(false);

        yield return new WaitForSeconds(0.3f);
        state = State.Cooloff;
        ActionComplete();
    }

    public override string GetActionName() => "Shoot";

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        GridPosition unitGridPosition = unit.GetGridPosition();
        return GetValidActionGridPositionList(unitGridPosition);
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
                    if (LevelGrid.Instance.IsValidGridPosition(testGridPosition))
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

        targetUnit = targetUnitList[0];
        state = State.Aiming;
        ActionStart(onShootComplete);
        StartCoroutine(StateCheck());
    }

    public void ShootArrow()
    {
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
        targetUnit.Damage(shootDamage);
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        Unit targetUnit = LevelGrid.Instance.GetUnitListAtGridPosition(gridPosition)[0];

        // AI �켱���� ��� (�� ü���� ��������, ���� ���� ��ġ �ο�)
        int actionValue = (targetUnit.GetTeamType() != TeamType.Player) ?
            500 + (100 - Mathf.RoundToInt(targetUnit.GetHealthNormalized() * 100f)) :
            1000 + (100 - Mathf.RoundToInt(targetUnit.GetHealthNormalized() * 100f));

        return new EnemyAIAction { gridPosition = gridPosition, actionValue = actionValue };
    }

    public int GetTargetCountAtPosition(GridPosition gridPosition) => GetValidActionGridPositionList(gridPosition).Count;
    public Unit GetTargetUnit() => targetUnit;
    public int GetMaxShootDistance() => maxShootDistance;
}
