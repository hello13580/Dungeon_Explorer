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
    protected override string DefaultActionName() => "회오리";
    public override ActionCategory GetActionCategory() => ActionCategory.Attack;
    [Header("데미지")]
    [SerializeField] private int damage = 8;
    [SerializeField] private int hitCount = 4;
    [SerializeField] private float hitForce = 300f;

    [Header("회전")]
    [SerializeField] private float spinDuration = 1.5f;
    [SerializeField] private float spinSpeed = 720f;

    [Header("윤곽선")]
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private Color lineColor = new Color(0.3f, 1f, 0.3f, 0.9f);
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float heightOffset = 0.1f;

    [Header("트레일")]
    [SerializeField] private int trailCount = 3;
    [SerializeField] private float trailRadius = 0.6f;
    [SerializeField] private float trailHeight = 1f;
    [SerializeField] private float trailTime = 0.25f;
    [SerializeField] private float trailStartWidth = 0.15f;
    [SerializeField] private float trailEndWidth = 0f;
    [SerializeField] private Color trailColor = new Color(1f, 0.4f, 0.1f, 1f);
    [SerializeField] private Material trailMaterial;

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
        ShowOutline();

        // 트레일 포인트 생성
        GameObject[] trailPoints = CreateTrailPoints();

        float elapsed = 0f;
        float interval = spinDuration / hitCount;
        float nextHitTime = interval;
        int hitsDealt = 0;
        float orbitAngle = 0f;

        while (elapsed < spinDuration)
        {
            transform.eulerAngles += new Vector3(0f, spinSpeed * Time.deltaTime, 0f);
            elapsed += Time.deltaTime;
            orbitAngle += spinSpeed * Time.deltaTime;

            // 트레일 포인트를 유닛 주위로 공전
            UpdateTrailPoints(trailPoints, orbitAngle);

            if (hitsDealt < hitCount && elapsed >= nextHitTime)
            {
                DamageNearbyEnemies();
                hitsDealt++;
                nextHitTime += interval;
            }

            yield return null;
        }

        while (hitsDealt < hitCount)
        {
            DamageNearbyEnemies();
            hitsDealt++;
        }

        lineRenderer.enabled = false;

        // 트레일이 자연스럽게 사라지도록 잠시 후 제거
        foreach (GameObject tp in trailPoints)
            if (tp != null) Destroy(tp, trailTime + 0.1f);

        OnWhirlwindEnded?.Invoke(this, EventArgs.Empty);
        ActionComplete();
    }

    private GameObject[] CreateTrailPoints()
    {
        GameObject[] points = new GameObject[trailCount];
        Vector3 center = unit.GetWorldPosition() + Vector3.up * trailHeight;

        for (int i = 0; i < trailCount; i++)
        {
            GameObject obj = new GameObject($"WhirlwindTrail_{i}");
            float startAngle = (360f / trailCount) * i;
            float rad = startAngle * Mathf.Deg2Rad;
            obj.transform.position = center + new Vector3(Mathf.Sin(rad) * trailRadius, 0f, Mathf.Cos(rad) * trailRadius);

            TrailRenderer trail = obj.AddComponent<TrailRenderer>();
            trail.time = trailTime;
            trail.startWidth = trailStartWidth;
            trail.endWidth = trailEndWidth;
            trail.material = trailMaterial != null ? trailMaterial : new Material(Shader.Find("Sprites/Default"));

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(trailColor, 0f),
                    new GradientColorKey(trailColor, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            trail.colorGradient = gradient;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            points[i] = obj;
        }
        return points;
    }

    private void UpdateTrailPoints(GameObject[] points, float orbitAngle)
    {
        Vector3 center = unit.GetWorldPosition() + Vector3.up * trailHeight;
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null) continue;
            float angle = orbitAngle + (360f / trailCount) * i;
            float rad = angle * Mathf.Deg2Rad;
            points[i].transform.position = center + new Vector3(Mathf.Sin(rad) * trailRadius, 0f, Mathf.Cos(rad) * trailRadius);
        }
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
        HashSet<Unit> alreadyHit = new HashSet<Unit>();

        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                if (x == 0 && z == 0) continue;

                GridPosition testPos = myPos + new GridPosition(x, z, 0);
                if (!LevelGrid.Instance.IsValidGridPosition(testPos)) continue;
                if (!LevelGrid.Instance.IsGridPositionOccupied(testPos)) continue;

                Unit target = LevelGrid.Instance.GetUnitListAtGridPosition(testPos)[0];
                if (!TeamHelper.IsHostile(unit.GetTeamType(), target.GetTeamType())) continue;
                if (alreadyHit.Contains(target)) continue;
                alreadyHit.Add(target);

                Vector3 hitDir = (target.GetWorldPosition() - unit.GetWorldPosition()).normalized;
                target.GetHitReaction().SetHitDirection(hitDir);
                target.GetHitReaction().SetHitForce(hitForce);
                target.Damage(unit.CalculateDamage(damage));
            }
        }
    }

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        return new List<GridPosition> { unit.GetGridPosition() };
    }

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
