using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BarrierAuraAction이 활성화된 동안 실제 범위 타일 경계에 맞는 윤곽선을 그린다.
///
/// cachedPath는 그리드 절대 좌표 기반이므로 유닛이 이동하면 새 위치 기준으로 재계산한다.
/// LateUpdate에서는 이동 중 transform.position 오프셋만 적용해 부드럽게 따라오게 한다.
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
    private BarrierAuraAction auraAction;

    // 현재 path가 어느 그리드 위치 기준인지 기억
    private List<Vector2Int> cachedPath;
    private GridPosition cachedCenter;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        auraAction = GetComponent<BarrierAuraAction>();
        GridOutlineUtil.SetupLineRenderer(lineRenderer, lineWidth, lineColor, lineMaterial);
        lineRenderer.enabled = false;
    }

    private void Start()
    {
        if (auraAction == null) return;
        auraAction.OnAuraActivated   += (s, e) => { lineRenderer.enabled = true;  RebuildPath(); };
        auraAction.OnAuraDeactivated += (s, e) => { lineRenderer.enabled = false; cachedPath = null; };

        // 이동 등 액션이 끝나면 그리드 위치가 바뀌었을 수 있으므로 path 재계산
        BaseAction.OnAnyActionEnded += (s, e) => { if (lineRenderer.enabled) RebuildPath(); };
    }

    private void OnDestroy()
    {
        BaseAction.OnAnyActionEnded -= (s, e) => { if (lineRenderer.enabled) RebuildPath(); };
    }

    private void LateUpdate()
    {
        if (!lineRenderer.enabled || cachedPath == null) return;

        // 이동 중 transform.position과 그리드 스냅 위치의 차이를 오프셋으로 적용
        Vector3 gridSnappedCenter = LevelGrid.Instance.GetWorldPosition(cachedCenter);
        Vector3 movementOffset = transform.position - gridSnappedCenter;
        movementOffset.y = 0f;

        GridOutlineUtil.ApplyPathToLineRenderer(lineRenderer, cachedPath, heightOffset, movementOffset);
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
        cachedCenter = center; // 이 path가 어느 위치 기준인지 저장
    }
}
