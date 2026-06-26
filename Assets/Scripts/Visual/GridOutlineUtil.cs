using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 그리드 타일 집합의 외곽 윤곽선을 계산하는 공통 유틸리티.
/// AuraVisual, DamageZoneVisual 등에서 재사용한다.
/// </summary>
public static class GridOutlineUtil
{
    /// <summary>
    /// 타일 집합의 외곽 엣지를 연결된 경로(하프셀 좌표)로 반환한다.
    /// </summary>
    public static List<Vector2Int> BuildOutlinePath(HashSet<Vector2Int> tileSet)
    {
        var edges = new Dictionary<Vector2Int, Vector2Int>();

        foreach (Vector2Int t in tileSet)
        {
            int hx = t.x * 2;
            int hz = t.y * 2;

            if (!tileSet.Contains(new Vector2Int(t.x + 1, t.y)))
                edges[new Vector2Int(hx + 2, hz)] = new Vector2Int(hx + 2, hz + 2);

            if (!tileSet.Contains(new Vector2Int(t.x - 1, t.y)))
                edges[new Vector2Int(hx, hz + 2)] = new Vector2Int(hx, hz);

            if (!tileSet.Contains(new Vector2Int(t.x, t.y + 1)))
                edges[new Vector2Int(hx + 2, hz + 2)] = new Vector2Int(hx, hz + 2);

            if (!tileSet.Contains(new Vector2Int(t.x, t.y - 1)))
                edges[new Vector2Int(hx, hz)] = new Vector2Int(hx + 2, hz);
        }

        if (edges.Count == 0) return new List<Vector2Int>();
        return TraceEdgePath(edges);
    }

    /// <summary>
    /// 하프셀 경로를 월드 좌표로 변환해 LineRenderer에 적용한다.
    /// </summary>
    public static void ApplyPathToLineRenderer(
        LineRenderer lr,
        List<Vector2Int> path,
        float heightOffset,
        Vector3 worldOffset = default,
        float referenceY = float.MinValue)
    {
        if (path == null || path.Count == 0)
        {
            lr.positionCount = 0;
            return;
        }

        float cellSize = LevelGrid.Instance.GetCellSize();
        float half = cellSize * 0.5f;
        Vector3 gridOrigin = LevelGrid.Instance.GetWorldPosition(new GridPosition(0, 0, 0));

        // referenceY가 지정되지 않으면 gridOrigin.y 사용 (기존 동작 유지)
        float baseY = (referenceY > float.MinValue) ? referenceY : gridOrigin.y;

        lr.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++)
        {
            float wx = gridOrigin.x - half + path[i].x * half + worldOffset.x;
            float wz = gridOrigin.z - half + path[i].y * half + worldOffset.z;
            float wy = baseY + heightOffset + worldOffset.y;

            // 기준 Y보다 4f 위에서 아래로 레이캐스트 (층 높이 대응)
            if (Physics.Raycast(new Vector3(wx, baseY + 4f, wz), Vector3.down, out RaycastHit hit, 8f))
                wy = hit.point.y + heightOffset;

            lr.SetPosition(i, new Vector3(wx, wy, wz));
        }
    }

    /// <summary>
    /// LineRenderer 기본 스타일 설정.
    /// </summary>
    public static void SetupLineRenderer(LineRenderer lr, float width, Color color, Material material)
    {
        lr.loop = false;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.useWorldSpace = true;
        lr.material = material != null
            ? material
            : new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;
        lr.sortingOrder = 1;
    }

    private static List<Vector2Int> TraceEdgePath(Dictionary<Vector2Int, Vector2Int> edges)
    {
        var path = new List<Vector2Int>();
        Vector2Int start = default;
        foreach (var key in edges.Keys) { start = key; break; }

        Vector2Int current = start;
        int safety = edges.Count + 2;

        while (safety-- > 0)
        {
            path.Add(current);
            if (!edges.TryGetValue(current, out Vector2Int next)) break;
            edges.Remove(current);
            current = next;
            if (current == start) break;
        }

        path.Add(start);
        return path;
    }
}
