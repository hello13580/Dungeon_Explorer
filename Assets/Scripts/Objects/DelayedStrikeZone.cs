using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 시전 후 N턴 뒤에 터지는 지연 폭발 장판.
/// 영향 범위를 빨간 외곽선으로 표시하고, 범위 중앙에 남은 턴 수를 숫자로 띄운다.
/// 시전자 팀의 턴이 시작될 때마다 카운트다운하며, 0이 되는 순간 그 시점에 범위 안에 있는
/// 모든 적에게 피해를 입히고 사라진다(설치/진입/틱 피해는 없음 — 오직 마지막 한 방).
/// </summary>
public class DelayedStrikeZone : MonoBehaviour
{
    public static event EventHandler OnAnyDelayedStrikeZoneCreated;
    public static event EventHandler OnAnyDelayedStrikeZoneDestroyed;

    private List<GridPosition> affectedPositions;
    private int damage;
    private int turnsRemaining;
    private TeamType ownerTeamType;

    private LineRenderer lineRenderer;
    private TextMeshPro countdownText;

    public void Setup(
        List<GridPosition> positions, int damage, int duration, TeamType ownerTeamType, int attackPower,
        float outlineWidth, Color outlineColor, Material outlineMaterial, float outlineHeightOffset,
        float textHeightOffset, Color textColor, float fontSize)
    {
        affectedPositions = positions;
        this.damage = damage + attackPower;
        turnsRemaining = duration;
        this.ownerTeamType = ownerTeamType;

        BuildOutline(outlineWidth, outlineColor, outlineMaterial, outlineHeightOffset);
        BuildCountdownText(textHeightOffset, textColor, fontSize);

        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
        StageManager.OnStageLoadingStarted += StageManager_OnStageLoadingStarted;
        OnAnyDelayedStrikeZoneCreated?.Invoke(this, EventArgs.Empty);
    }

    private void OnDestroy()
    {
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
        StageManager.OnStageLoadingStarted -= StageManager_OnStageLoadingStarted;
    }

    private void StageManager_OnStageLoadingStarted(object sender, EventArgs e)
    {
        OnAnyDelayedStrikeZoneDestroyed?.Invoke(this, EventArgs.Empty);
        Destroy(gameObject);
    }

    // ─── 비주얼 구성 ─────────────────────────────────────────────────

    private void BuildOutline(float width, Color color, Material material, float heightOffset)
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        GridOutlineUtil.SetupLineRenderer(lineRenderer, width, color, material);

        HashSet<Vector2Int> tileSet = new HashSet<Vector2Int>();
        foreach (GridPosition pos in affectedPositions)
            tileSet.Add(new Vector2Int(pos.x, pos.z));

        List<Vector2Int> path = GridOutlineUtil.BuildOutlinePath(tileSet);
        GridOutlineUtil.ApplyPathToLineRenderer(lineRenderer, path, heightOffset);
        lineRenderer.enabled = true;
    }

    private void BuildCountdownText(float heightOffset, Color color, float fontSize)
    {
        Vector3 center = GetAreaCenterWorldPosition() + Vector3.up * heightOffset;

        GameObject textObj = new GameObject("CountdownText");
        textObj.transform.position = center;

        countdownText = textObj.AddComponent<TextMeshPro>();
        countdownText.text = turnsRemaining.ToString();
        countdownText.fontSize = fontSize;
        countdownText.color = color;
        countdownText.alignment = TextAlignmentOptions.Center;

        // 항상 카메라를 바라보도록 빌보드 처리
        textObj.AddComponent<UILookAtCamera>();
    }

    private Vector3 GetAreaCenterWorldPosition()
    {
        if (affectedPositions == null || affectedPositions.Count == 0) return transform.position;

        Vector3 sum = Vector3.zero;
        foreach (GridPosition pos in affectedPositions)
            sum += LevelGrid.Instance.GetWorldPosition(pos);
        return sum / affectedPositions.Count;
    }

    // ─── 턴 처리 ───────────────────────────────────────────────────────

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        // 시전자 팀 턴 한정이 아니라, 모든 유닛의 턴이 끝날 때마다(OnTurnChanged 발생마다) 카운트다운
        turnsRemaining--;
        if (countdownText != null)
            countdownText.text = Mathf.Max(turnsRemaining, 0).ToString();

        if (turnsRemaining <= 0)
            Explode();
    }

    private void Explode()
    {
        OnAnyDelayedStrikeZoneDestroyed?.Invoke(this, EventArgs.Empty);
        TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;

        HashSet<Unit> hitUnits = new HashSet<Unit>();
        foreach (GridPosition pos in affectedPositions)
        {
            if (!LevelGrid.Instance.IsValidGridPosition(pos)) continue;
            foreach (Unit u in LevelGrid.Instance.GetUnitListAtGridPosition(pos))
            {
                if (!TeamHelper.IsHostile(ownerTeamType, u.GetTeamType())) continue;
                if (!hitUnits.Add(u)) continue; // 사이즈 2 이상 유닛 중복 방지
                u.Damage(damage);
            }
        }

        Destroy(gameObject);
    }

    public List<GridPosition> GetAffectedPositions() => affectedPositions;
    public int GetTurnsRemaining() => turnsRemaining;
}
