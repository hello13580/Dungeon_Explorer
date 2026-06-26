using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도약 — 목표 위치까지 포물선 궤적으로 점프해 이동한다.
/// 고저차(층 차이)만큼 사거리를 소모하므로 사거리보다 높거나 낮은 곳은 도달 불가.
/// 유효 범위: 수평 거리 + |층 차이| <= maxRange
/// </summary>
public class LeapAction : BaseAction
{
    [Header("사거리")]
    [SerializeField] private int maxRange = 5;

    [Header("점프")]
    [SerializeField] private float jumpDuration = 0.6f;        // 이동에 걸리는 시간 (초)
    [SerializeField] private float jumpArcHeight = 3f;         // 포물선 최고점 높이
    [SerializeField] private float landingAnimLeadTime = 0.3f; // 착지 몇 초 전에 JumpEnd 애니메이션 발동

    public event EventHandler<GridPosition> OnLeapStarted;   // JumpStart 애니메이션 재생용
    public event EventHandler<GridPosition> OnLeapLanding;   // JumpEnd 애니메이션 재생용 (착지 직전)
    public event EventHandler<GridPosition> OnLeapLanded;    // 착지 완료

    // 유효 위치 캐시
    private List<GridPosition> cachedValidList;
    private bool isCacheDirty = true;

    // 애니메이션 이벤트에서 호출 — 이 시점부터 실제 이동 시작
    private bool leapMovementReady = false;
    public void StartLeapMovement() => leapMovementReady = true;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }

    private void Start()
    {
        TurnSystem.Instance.OnTurnChanged += OnCacheInvalidated;
        BaseAction.OnAnyActionEnded += OnCacheInvalidated;
    }

    private void OnDestroy()
    {
        TurnSystem.Instance.OnTurnChanged -= OnCacheInvalidated;
        BaseAction.OnAnyActionEnded -= OnCacheInvalidated;
    }

    private void OnCacheInvalidated(object sender, EventArgs e) => isCacheDirty = true;

    public override string GetActionName() => "Leap";

    public override string GetDescription() =>
        $"사거리 {maxRange}칸 내 빈 타일로 점프해 이동한다. 고저차 1층당 사거리 1 소모.";

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        ActionStart(onActionComplete);
        StartCoroutine(LeapRoutine(gridPosition));
    }

    private IEnumerator LeapRoutine(GridPosition targetGridPosition)
    {
        // 1. 목표 방향으로 즉시 회전
        Vector3 targetWorldPos = LevelGrid.Instance.GetWorldPosition(targetGridPosition);
        Vector3 lookDir = targetWorldPos - transform.position;
        lookDir.y = 0f;
        if (lookDir != Vector3.zero)
            transform.forward = lookDir.normalized;

        // 2. 이동 시작 위치를 애니메이션 재생 전에 확정 — Root Motion으로 위치가 바뀌기 전 값을 사용
        Vector3 startPos = transform.position;

        // 3. JumpStart 애니메이션 재생 — 애니메이션 이벤트(StartLeapMovement)가 올 때까지 대기
        leapMovementReady = false;
        OnLeapStarted?.Invoke(this, targetGridPosition);
        yield return new WaitUntil(() => leapMovementReady);

        // Root Motion이 위치를 변경했을 수 있으므로 저장해둔 startPos로 복원
        transform.position = startPos;

        // 착지 지점 계산 — 지면 높이에 스냅
        float cellSize = LevelGrid.Instance.GetCellSize();
        float centerOffset = (unit.GetSize() - 1) * cellSize * 0.5f;
        Vector3 offsetVector = new Vector3(centerOffset, 0f, centerOffset);
        Vector3 endPos = LevelGrid.Instance.GetWorldPosition(targetGridPosition) + offsetVector;

        LayerMask snapMask = unit.GetGroundSnapLayerMask();
        if (snapMask != 0 &&
            Physics.Raycast(endPos + Vector3.up * 2f, Vector3.down, out RaycastHit groundHit, 4f, snapMask))
        {
            endPos.y = groundHit.point.y;
        }

        // 3. 포물선 이동 — jumpDuration 중 landingAnimLeadTime 전에 JumpEnd 애니메이션 발동
        float elapsed = 0f;
        bool landingTriggered = false;
        while (elapsed < jumpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / jumpDuration);

            // 포물선 궤적: 수평은 선형, 수직은 sin 곡선으로 아치 형성
            Vector3 flatPos = Vector3.Lerp(startPos, endPos, t);
            float arcY = Mathf.Sin(t * Mathf.PI) * jumpArcHeight;
            transform.position = new Vector3(flatPos.x, flatPos.y + arcY, flatPos.z);

            // 착지 직전 JumpEnd 애니메이션 발동
            if (!landingTriggered && elapsed >= jumpDuration - landingAnimLeadTime)
            {
                OnLeapLanding?.Invoke(this, targetGridPosition);
                landingTriggered = true;
            }

            yield return null;
        }

        // 4. 착지 보정
        transform.position = endPos;
        unit.SetGridPosition(targetGridPosition);

        OnLeapLanded?.Invoke(this, targetGridPosition);
        ActionComplete();
    }

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        if (!isCacheDirty && cachedValidList != null) return cachedValidList;

        GridPosition unitPos = unit.GetGridPosition();
        int unitSize = unit.GetSize();
        int floorAmount = LevelGrid.Instance.GetFloorAmount();

        // 층 차이가 maxRange 이내인 층만 탐색
        int minFloor = Mathf.Max(0, unitPos.floor - maxRange);
        int maxFloor = Mathf.Min(floorAmount - 1, unitPos.floor + maxRange);

        List<GridPosition> validList = new List<GridPosition>();

        for (int x = -maxRange; x <= maxRange; x++)
        {
            for (int z = -maxRange; z <= maxRange; z++)
            {
                for (int floor = minFloor; floor <= maxFloor; floor++)
                {
                    int floorDiff = Mathf.Abs(floor - unitPos.floor);

                    // 수평 거리 + 층 차이 <= maxRange
                    float horizontalDist = Mathf.Sqrt(x * x + z * z);
                    if (horizontalDist + floorDiff > maxRange) continue;

                    GridPosition testPos = new GridPosition(
                        unitPos.x + x,
                        unitPos.z + z,
                        floor
                    );

                    if (!LevelGrid.Instance.IsValidGridPosition(testPos)) continue;
                    // [문제 해결] IsWalkableGridPosition → IsDirectlyTargetable로 교체
                    // 계단 exclusive 타일은 IsWalkable=false지만 착지 후 계단으로 나갈 수 있으므로
                    // 점프 착지 지점으로는 유효하다. (PathFinding.IsDirectlyTargetable 주석 참고)
                    if (!PathFinding.Instance.IsDirectlyTargetable(testPos)) continue;
                    if (testPos == unitPos) continue;

                    // 다른 유닛이 점유한 타일 제외
                    bool occupied = false;
                    for (int i = 0; i < unitSize && !occupied; i++)
                        for (int j = 0; j < unitSize && !occupied; j++)
                        {
                            GridPosition subPos = testPos + new GridPosition(i, j, 0);
                            if (LevelGrid.Instance.IsGridPositionOccupied(subPos))
                            {
                                Unit occupant = LevelGrid.Instance.GetUnitListAtGridPosition(subPos)[0];
                                if (occupant != unit) occupied = true;
                            }
                        }

                    if (!occupied) validList.Add(testPos);
                }
            }
        }

        cachedValidList = validList;
        isCacheDirty = false;
        return cachedValidList;
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        // 도약 후 근접 공격 사정거리 안에 적이 있는 위치를 선호
        MeleeAction meleeAction = unit.GetAction<MeleeAction>();
        if (meleeAction != null)
        {
            float minDist = float.MaxValue;
            foreach (Unit playerUnit in UnitManager.Instance.GetFriendlyUnitList())
            {
                if (playerUnit.IsStealthed()) continue;
                float dist = Vector3.Distance(
                    LevelGrid.Instance.GetWorldPosition(gridPosition),
                    playerUnit.GetWorldPosition()
                );
                if (dist < minDist) minDist = dist;
            }
            int value = Mathf.RoundToInt(50f - minDist * 5f);
            return new EnemyAIAction { gridPosition = gridPosition, actionValue = Mathf.Clamp(value, 0, 49) };
        }

        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 10 };
    }
}
