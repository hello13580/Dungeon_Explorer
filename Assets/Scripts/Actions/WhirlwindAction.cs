using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 휠윈드 — 유닛이 회전하면서 주변 1칸 내 모든 적에게 hitCount번 피해를 준다.
/// 총 spinDuration초 동안 hitCount번 균등한 간격으로 피해를 입힌다.
/// 실행 중에는 주변 8칸 경계에 LineRenderer로 윤곽선을 표시한다.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class WhirlwindAction : BaseAction
{
    [Header("데미지")]
    [SerializeField] private int damage = 8;
    [SerializeField] private int hitCount = 4;          // 총 피해 횟수
    [SerializeField] private float hitForce = 300f;

    [Header("회전")]
    [SerializeField] private float spinDuration = 1.5f; // 총 회전 시간 (초)
    [SerializeField] private float spinSpeed = 720f;    // 초당 회전 각도

    [Header("윤곽선")]
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private Color lineColor = new Color(0.3f, 1f, 0.3f, 0.9f);
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float heightOffset = 0.1f;

    public event EventHandler OnWhirlwindStarted;
    public event EventHandler OnWhirlwindEnded;

    private LineRenderer lineRenderer;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;

        lineRenderer = GetComponent<LineRenderer>();
        GridOutlineUtil.SetupLineRenderer(lineRenderer, lineWidth, lineColor, lineMaterial);
        lineRenderer.enabled = false;
    }

    public override string GetActionName() => "Whirlwind";

    public override string GetDescription() =>
        $"주변 1칸 내 모든 적에게 {hitCount}번에 걸쳐 각 {unit.CalculateDamage(damage)} 피해를 입힌다. (총 {unit.CalculateDamage(damage) * hitCount})";

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        ActionStart(onActionComplete);
        StartCoroutine(WhirlwindRoutine());
    }

    private IEnumerator WhirlwindRoutine()
    {
        OnWhirlwindStarted?.Invoke(this, EventArgs.Empty);

        // 주변 8칸 윤곽선 표시
        ShowOutline();

        float elapsed = 0f;
        float interval = spinDuration / hitCount; // 피해 간격
        float nextHitTime = interval;
        int hitsDealt = 0;

        while (elapsed < spinDuration)
        {
            // 유닛 회전
            transform.eulerAngles += new Vector3(0f, spinSpeed * Time.deltaTime, 0f);
            elapsed += Time.deltaTime;

            // 일정 간격마다 주변 적에게 피해
            if (hitsDealt < hitCount && elapsed >= nextHitTime)
            {
                DamageNearbyEnemies();
                hitsDealt++;
                nextHitTime += interval;
            }

            yield return null;
        }

        // 마지막 틱이 누락되지 않도록 보장
        while (hitsDealt < hitCount)
        {
            DamageNearbyEnemies();
            hitsDealt++;
        }

        // 회전 종료 후 윤곽선 제거
        lineRenderer.enabled = false;

        OnWhirlwindEnded?.Invoke(this, EventArgs.Empty);
        ActionComplete();
    }

    private void ShowOutline()
    {
        GridPosition center = unit.GetGridPosition();
        HashSet<Vector2Int> tileSet = new HashSet<Vector2Int>();

        for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && z == 0) continue;
                GridPosition pos = center + new GridPosition(x, z, 0);
                if (LevelGrid.Instance.IsValidGridPosition(pos))
                    tileSet.Add(new Vector2Int(pos.x, pos.z));
            }

        List<Vector2Int> path = GridOutlineUtil.BuildOutlinePath(tileSet);
        GridOutlineUtil.ApplyPathToLineRenderer(lineRenderer, path, heightOffset);
        lineRenderer.enabled = true;
    }

    private void DamageNearbyEnemies()
    {
        GridPosition myPos = unit.GetGridPosition();

        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && z == 0) continue; // 자기 자신 제외

                GridPosition testPos = myPos + new GridPosition(x, z, 0);
                if (!LevelGrid.Instance.IsValidGridPosition(testPos)) continue;
                if (!LevelGrid.Instance.IsGridPositionOccupied(testPos)) continue;

                Unit target = LevelGrid.Instance.GetUnitListAtGridPosition(testPos)[0];
                if (!TeamHelper.IsHostile(unit.GetTeamType(), target.GetTeamType())) continue;

                Vector3 hitDir = (target.GetWorldPosition() - unit.GetWorldPosition()).normalized;
                target.GetHitReaction().SetHitDirection(hitDir);
                target.GetHitReaction().SetHitForce(hitForce);
                target.Damage(unit.CalculateDamage(damage));
            }
        }
    }

    // 휠윈드는 자기 자신 위치를 선택하는 셀프 캐스트
    public override List<GridPosition> GetValidActionGridPositionList()
    {
        return new List<GridPosition> { unit.GetGridPosition() };
    }

    // 그리드 비주얼에서 빨간색으로 표시할 피해 범위 (주변 8칸)
    public override List<GridPosition> GetDamageAffectedGridPosition(GridPosition targetGridPosition)
    {
        List<GridPosition> affected = new List<GridPosition>();
        GridPosition myPos = unit.GetGridPosition();

        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && z == 0) continue;
                GridPosition testPos = myPos + new GridPosition(x, z, 0);
                if (LevelGrid.Instance.IsValidGridPosition(testPos))
                    affected.Add(testPos);
            }
        }
        return affected;
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        // 주변에 적이 많을수록 가치 높음
        int enemyCount = 0;
        GridPosition myPos = unit.GetGridPosition();
        for (int x = -1; x <= 1; x++)
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && z == 0) continue;
                GridPosition testPos = myPos + new GridPosition(x, z, 0);
                if (!LevelGrid.Instance.IsValidGridPosition(testPos)) continue;
                if (!LevelGrid.Instance.IsGridPositionOccupied(testPos)) continue;
                Unit target = LevelGrid.Instance.GetUnitListAtGridPosition(testPos)[0];
                if (TeamHelper.IsHostile(unit.GetTeamType(), target.GetTeamType()))
                    enemyCount++;
            }
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = enemyCount * 100 };
    }
}
