using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// IAuraAction을 구현한 오라 액션이 활성화된 동안 범위 타일 경계에 맞는 윤곽선을 그린다.
/// BarrierAuraAction, AttackAuraAction 등 어떤 오라 계열 액션이든 하나의 컴포넌트로 처리한다.
///
/// 같은 유닛에 오라 액션이 여러 개 있을 경우 각각 AuraVisual을 추가하면 된다.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class AuraVisual : MonoBehaviour
{
    [Header("선 스타일")]
    [SerializeField] private float lineWidth = 0.08f;
    [SerializeField] private Color lineColor = new Color(0.4f, 0.8f, 1f, 0.85f);
    [SerializeField] private Material lineMaterial;
    [SerializeField] private float heightOffset = 0.1f;

    private LineRenderer lineRenderer;
    private IAuraAction auraAction;

    private List<Vector2Int> cachedPath;
    private GridPosition cachedCenter;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        // 같은 GameObject의 IAuraAction 구현체를 자동으로 찾는다
        auraAction = GetComponent<IAuraAction>();
        GridOutlineUtil.SetupLineRenderer(lineRenderer, lineWidth, lineColor, lineMaterial);
        lineRenderer.enabled = false;
    }

    private void Start()
    {
        if (auraAction == null) return;
        auraAction.OnAuraActivated   += (s, e) => { lineRenderer.enabled = true;  RebuildPath(); };
        auraAction.OnAuraDeactivated += (s, e) => { lineRenderer.enabled = false; cachedPath = null; };

        // 이동 등 액션이 끝나면 그리드 위치가 바뀌었을 수 있으므로 path 재계산
        BaseAction.OnAnyActionEnded += OnAnyActionEnded;
    }

    private void OnDestroy()
    {
        BaseAction.OnAnyActionEnded -= OnAnyActionEnded;
    }

    private void OnAnyActionEnded(object sender, EventArgs e)
    {
        if (lineRenderer.enabled) RebuildPath();
    }

    private void LateUpdate()
    {
        if (!lineRenderer.enabled || cachedPath == null) return;

        // 이동 중 transform.position과 그리드 스냅 위치의 차이를 오프셋으로 적용
        Vector3 gridSnappedCenter = LevelGrid.Instance.GetWorldPosition(cachedCenter);
        Vector3 movementOffset = transform.position - gridSnappedCenter;
        movementOffset.y = 0f;

        GridOutlineUtil.ApplyPathToLineRenderer(lineRenderer, cachedPath, heightOffset, movementOffset, transform.position.y);
    }

    private void RebuildPath()
    {
        GridPosition center = auraAction.GetUnit().GetGridPosition();
        int range = auraAction.GetAuraRange();

        HashSet<Vector2Int> tileSet = new HashSet<Vector2Int>();
        for (int x = -range; x <= range; x++)
        {
            for (int z = -range; z <= range; z++)
            {
                if (Mathf.Sqrt(x * x + z * z) > range) continue;
                GridPosition pos = center + new GridPosition(x, z, 0);
                if (LevelGrid.Instance.IsValidGridPosition(pos))
                    tileSet.Add(new Vector2Int(pos.x, pos.z));
            }
        }

        cachedPath = GridOutlineUtil.BuildOutlinePath(tileSet);
        cachedCenter = center;
    }
}
