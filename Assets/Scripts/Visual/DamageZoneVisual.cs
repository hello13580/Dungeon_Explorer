using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DamageZone이 활성화된 동안 장판 범위 경계에 맞는 윤곽선을 그린다.
/// DamageZone 프리팹에 추가하면 자동으로 연결된다.
///
/// DamageZone.OnAnyDamageZoneCreated 이벤트를 사용해 Setup() 완료 직후에 윤곽선을 생성한다.
/// Start()나 Awake() 타이밍 문제를 완전히 우회한다.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class DamageZoneVisual : MonoBehaviour
{
    [Header("선 스타일")]
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private Color lineColor = new Color(1f, 0.4f, 0.1f, 0.9f);
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float heightOffset = 0.1f;

    private LineRenderer lineRenderer;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        GridOutlineUtil.SetupLineRenderer(lineRenderer, lineWidth, lineColor, lineMaterial);
        lineRenderer.enabled = false;

        // Setup() 내부에서 발생하는 이벤트를 구독 — 이 시점에 affectedPositions가 확실히 설정됨
        DamageZone.OnAnyDamageZoneCreated += OnAnyDamageZoneCreated;
    }

    private void OnDestroy()
    {
        DamageZone.OnAnyDamageZoneCreated -= OnAnyDamageZoneCreated;
    }

    private void OnAnyDamageZoneCreated(object sender, EventArgs e)
    {
        // 이 GameObject의 DamageZone인지 확인
        if (sender is not DamageZone zone || zone.gameObject != gameObject) return;

        // 한 번만 처리하면 되므로 구독 해제
        DamageZone.OnAnyDamageZoneCreated -= OnAnyDamageZoneCreated;

        BuildOutline(zone);
    }

    private void BuildOutline(DamageZone damageZone)
    {
        List<GridPosition> positions = damageZone.GetAffectedPositions();
        if (positions == null || positions.Count == 0) return;

        HashSet<Vector2Int> tileSet = new HashSet<Vector2Int>();
        foreach (GridPosition pos in positions)
            tileSet.Add(new Vector2Int(pos.x, pos.z));

        List<Vector2Int> path = GridOutlineUtil.BuildOutlinePath(tileSet);
        GridOutlineUtil.ApplyPathToLineRenderer(lineRenderer, path, heightOffset);
        lineRenderer.enabled = true;
    }
}
