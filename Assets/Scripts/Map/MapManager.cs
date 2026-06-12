using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 맵 진행 상태를 관리하는 DontDestroyOnLoad 싱글톤.
/// 현재 노드, 방문한 노드, 선택 가능한 노드를 추적한다.
/// </summary>
public class MapManager : MonoBehaviour
{
    public static MapManager Instance { get; private set; }

    /// <summary>현재 사용 중인 맵 데이터.</summary>
    public MapData CurrentMapData { get; private set; }

    /// <summary>현재 위치한 노드의 인덱스. -1이면 아직 시작 전.</summary>
    public int CurrentNodeIndex { get; private set; } = -1;

    // 방문 완료한 노드 인덱스 집합
    private HashSet<int> visitedNodes = new HashSet<int>();

    // 현재 선택 가능한(이동할 수 있는) 노드 인덱스 집합
    private HashSet<int> availableNodes = new HashSet<int>();

    /// <summary>노드 방문 후 맵 상태가 바뀔 때 발생. MapUI가 구독해 비주얼을 갱신한다.</summary>
    public static event EventHandler OnMapStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── 공개 API ────────────────────────────────────────────────

    /// <summary>
    /// 새 맵을 초기화한다. CharacterSelectManager.StartGame()에서 한 번만 호출한다.
    /// 이후에는 VisitNode()가 availableNodes를 갱신하므로 재호출하면 안 된다.
    /// </summary>
    public void InitializeMap(MapData mapData)
    {
        CurrentMapData = mapData;
        CurrentNodeIndex = -1;
        visitedNodes.Clear();
        availableNodes.Clear();

        // startNodeIndices에 등록된 노드들을 처음 선택 가능 상태로 설정
        foreach (int idx in mapData.startNodeIndices)
            availableNodes.Add(idx);

        OnMapStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 플레이어가 노드를 선택했을 때 호출.
    /// 방문 기록을 갱신하고 해당 노드의 nextNodeIndices를 다음 선택 가능 상태로 만든다.
    /// 이전 선택지(갈림길에서 선택 안 한 노드 포함)는 모두 선택 불가가 된다.
    /// </summary>
    public void VisitNode(int nodeIndex)
    {
        if (CurrentMapData == null) return;
        if (nodeIndex < 0 || nodeIndex >= CurrentMapData.nodes.Length) return;

        CurrentNodeIndex = nodeIndex;
        visitedNodes.Add(nodeIndex);

        // 이전 선택지는 더 이상 선택 불가 (이미 지나쳤거나 선택 안 한 갈림길)
        availableNodes.Clear();

        // 방문한 노드의 nextNodeIndices를 새 선택 가능 목록으로 설정
        MapNodeData node = CurrentMapData.nodes[nodeIndex];
        foreach (int nextIdx in node.nextNodeIndices)
            availableNodes.Add(nextIdx);

        OnMapStateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>해당 노드를 현재 선택할 수 있는지 반환.</summary>
    public bool IsNodeAvailable(int nodeIndex) => availableNodes.Contains(nodeIndex);

    /// <summary>해당 노드를 이미 방문했는지 반환.</summary>
    public bool IsNodeVisited(int nodeIndex) => visitedNodes.Contains(nodeIndex);

    /// <summary>다음 선택 가능한 노드가 없으면 맵 클리어 (보스 처치 등).</summary>
    public bool IsMapCleared() => availableNodes.Count == 0 && CurrentNodeIndex >= 0;
}
