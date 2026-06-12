using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 맵 선택 오버레이 패널.
/// - 게임 시작 시: CharacterSelectManager.StartGame() → OpenMapFromExternal() 으로 열린다.
/// - 스테이지 클리어 후: SkillUnlockManager.OnSkillUnlockCompleted → OpenMap() 으로 열린다.
/// 플레이어가 노드를 선택하면 해당 스테이지를 로드하고 패널을 닫는다.
/// </summary>
public class MapUI : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private GameObject panel;

    [Header("맵 데이터")]
    [SerializeField] private MapData mapData; // 이 게임에서 사용할 맵

    [Header("노드 UI")]
    [SerializeField] private Transform nodeContainer;    // 노드 버튼들이 배치될 부모
    [SerializeField] private GameObject mapNodePrefab;   // MapNodeUI 프리팹

    [Header("연결선 (선택)")]
    [SerializeField] private Transform lineContainer;    // 노드 간 선이 배치될 부모 (없으면 선 생략)
    [SerializeField] private GameObject linePrefab;      // 선 이미지 프리팹 (없으면 선 생략)

    // 생성된 노드 UI 목록 (상태 갱신 시 순회)
    private List<MapNodeUI> spawnedNodes = new List<MapNodeUI>();

    private void Start()
    {
        panel.SetActive(false);

        // 스킬 보상 창이 닫힌 직후 맵 패널을 연다
        SkillUnlockManager.OnSkillUnlockCompleted += OnSkillUnlockCompleted;

        // MapManager 상태가 바뀔 때(VisitNode 호출 등) 노드 비주얼을 갱신한다
        MapManager.OnMapStateChanged += OnMapStateChanged;
    }

    private void OnDestroy()
    {
        SkillUnlockManager.OnSkillUnlockCompleted -= OnSkillUnlockCompleted;
        MapManager.OnMapStateChanged -= OnMapStateChanged;
    }

    // ─── 이벤트 핸들러 ───────────────────────────────────────────

    private void OnSkillUnlockCompleted(object sender, System.EventArgs e)
    {
        // 스킬 보상 패널 닫힘 → 맵 패널 오픈
        OpenMap();
    }

    private void OnMapStateChanged(object sender, System.EventArgs e)
    {
        // 노드 비주얼 전체 갱신 (선택 가능 여부, 방문 표시 등)
        foreach (MapNodeUI nodeUI in spawnedNodes)
            nodeUI.RefreshState();
    }

    // ─── 맵 열기/닫기 ────────────────────────────────────────────

    /// <summary>
    /// CharacterSelectManager.StartGame()에서 호출.
    /// MapManager.InitializeMap()이 이미 호출된 상태에서 패널만 연다.
    /// </summary>
    public void OpenMapFromExternal()
    {
        BuildNodes();
        panel.SetActive(true);
    }

    /// <summary>
    /// 스킬 보상 완료 후 자동으로 호출되는 내부 오픈.
    /// MapManager가 아직 초기화되지 않은 경우(비정상 경로)에만 InitializeMap을 호출한다.
    /// 정상 경로에서는 CharacterSelectManager.StartGame()이 이미 초기화했으므로
    /// 진행 상태(visitedNodes, availableNodes)를 그대로 유지한다.
    /// </summary>
    private void OpenMap()
    {
        if (mapData == null)
        {
            Debug.LogWarning("[MapUI] MapData가 설정되지 않았습니다. 인스펙터에서 MapData를 연결해 주세요.");
            return;
        }

        // CurrentMapData가 null이면 초기화가 안 된 비정상 상태이므로 여기서 초기화한다.
        // 이미 초기화된 경우 재초기화하면 visitedNodes·availableNodes가 리셋되어
        // 이전에 방문한 노드가 다시 선택 가능해지는 버그가 발생하므로 절대 재호출하지 않는다.
        if (MapManager.Instance != null && MapManager.Instance.CurrentMapData == null)
            MapManager.Instance.InitializeMap(mapData);

        BuildNodes();
        panel.SetActive(true);
    }

    private void CloseMap()
    {
        panel.SetActive(false);
    }

    // ─── 노드 생성 ───────────────────────────────────────────────

    private void BuildNodes()
    {
        // 기존 노드 UI 제거 후 재생성 (스테이지 클리어마다 상태가 바뀌므로 매번 새로 만든다)
        foreach (Transform child in nodeContainer)
            Destroy(child.gameObject);
        spawnedNodes.Clear();

        if (mapData.nodes == null) return;

        for (int i = 0; i < mapData.nodes.Length; i++)
        {
            MapNodeData nodeData = mapData.nodes[i];
            if (nodeData == null) continue;

            GameObject nodeObj = Instantiate(mapNodePrefab, nodeContainer);
            MapNodeUI nodeUI = nodeObj.GetComponent<MapNodeUI>();

            // capturedIndex: 람다/클로저에서 루프 변수 i를 직접 참조하면 항상 마지막 값이
            // 캡처되므로 로컬 변수로 복사해서 각 노드가 올바른 인덱스를 갖도록 한다.
            int capturedIndex = i;
            nodeUI.Setup(capturedIndex, nodeData, OnNodeClicked);
            spawnedNodes.Add(nodeUI);
        }

        // 노드 생성 후 연결선 그리기 (linePrefab이 없으면 스킵)
        if (linePrefab != null && lineContainer != null)
            BuildLines();
    }

    /// <summary>노드 간 연결선을 이미지로 그린다. linePrefab이 없으면 스킵.</summary>
    private void BuildLines()
    {
        foreach (Transform child in lineContainer)
            Destroy(child.gameObject);

        for (int i = 0; i < mapData.nodes.Length; i++)
        {
            MapNodeData fromNode = mapData.nodes[i];
            foreach (int nextIdx in fromNode.nextNodeIndices)
            {
                if (nextIdx < 0 || nextIdx >= mapData.nodes.Length) continue;
                MapNodeData toNode = mapData.nodes[nextIdx];
                DrawLine(fromNode.position, toNode.position);
            }
        }
    }

    /// <summary>두 위치 사이에 선 이미지를 배치한다.</summary>
    private void DrawLine(Vector2 from, Vector2 to)
    {
        GameObject lineObj = Instantiate(linePrefab, lineContainer);
        RectTransform rt = lineObj.GetComponent<RectTransform>();
        if (rt == null) return;

        Vector2 dir = to - from;
        float distance = dir.magnitude;

        // 선의 중심을 두 노드의 중간 지점에 배치
        rt.anchoredPosition = (from + to) * 0.5f;

        // 선의 너비 = 두 노드 사이의 거리, 높이 = 선 두께 (기본 4px)
        rt.sizeDelta = new Vector2(distance, 4f);

        // 선의 방향으로 회전
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    // ─── 노드 클릭 처리 ──────────────────────────────────────────

    private void OnNodeClicked(int nodeIndex)
    {
        if (MapManager.Instance == null) return;

        // MapManager의 availableNodes에 없는 노드는 클릭해도 무시한다
        if (!MapManager.Instance.IsNodeAvailable(nodeIndex)) return;

        MapNodeData nodeData = mapData.nodes[nodeIndex];

        // 방문 기록 갱신 — availableNodes가 이 노드의 nextNodeIndices로 교체된다
        MapManager.Instance.VisitNode(nodeIndex);

        if (nodeData.stageData != null)
        {
            // 전투·엘리트·보스 노드: 맵을 닫고 해당 스테이지 로드
            CloseMap();
            StageManager.Instance.LoadStage(nodeData.stageData);
        }
        else
        {
            // Rest·Shop 등 전투가 없는 노드: 해당 기능 처리 후 맵 유지
            // TODO: Rest·Shop 기능 추가 시 여기에 구현
            Debug.Log($"[MapUI] 비전투 노드 선택: {nodeData.nodeType}");
        }
    }
}
