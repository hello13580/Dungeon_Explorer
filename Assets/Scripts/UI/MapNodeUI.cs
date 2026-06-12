using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 맵 UI에서 노드 하나를 담당하는 컴포넌트.
/// 노드 타입에 따른 아이콘·색상 표시, 선택 가능 여부에 따른 인터랙션 제어를 담당한다.
/// </summary>
public class MapNodeUI : MonoBehaviour
{
    [Header("표시 요소")]
    [SerializeField] private TextMeshProUGUI nodeTypeText;  // 노드 타입 이름 표시
    [SerializeField] private Image nodeIcon;                // 노드 타입 아이콘
    [SerializeField] private Image nodeBackground;          // 배경 이미지 (색상 변경용)
    [SerializeField] private GameObject visitedOverlay;     // 이미 방문한 노드에 표시할 오버레이
    [SerializeField] private GameObject currentOverlay;     // 현재 위치 노드에 표시할 오버레이
    [SerializeField] private Button nodeButton;

    [Header("노드 타입별 색상")]
    [SerializeField] private Color combatColor   = Color.red;
    [SerializeField] private Color eliteColor    = new Color(0.8f, 0.3f, 0.8f);
    [SerializeField] private Color bossColor     = new Color(0.6f, 0f, 0f);
    [SerializeField] private Color restColor     = Color.green;
    [SerializeField] private Color shopColor     = Color.yellow;

    private int nodeIndex;
    private Action<int> onNodeClicked; // 클릭 시 MapUI에 인덱스를 전달하는 콜백

    /// <summary>MapUI에서 호출. 노드 데이터와 클릭 콜백을 설정한다.</summary>
    public void Setup(int index, MapNodeData nodeData, Action<int> onClick)
    {
        nodeIndex = index;
        onNodeClicked = onClick;

        // 노드 타입 이름 표시
        if (nodeTypeText != null)
            nodeTypeText.text = GetNodeTypeName(nodeData.nodeType);

        // 노드 타입에 따른 배경 색상 변경
        if (nodeBackground != null)
            nodeBackground.color = GetNodeColor(nodeData.nodeType);

        // 버튼 클릭 리스너 등록
        if (nodeButton != null)
        {
            nodeButton.onClick.RemoveAllListeners();
            nodeButton.onClick.AddListener(OnButtonClicked);
        }

        // UI 위치 설정 (MapData에 지정된 픽셀 좌표로 이동)
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
            rt.anchoredPosition = nodeData.position;

        RefreshState();
    }

    /// <summary>MapManager 상태가 바뀔 때 MapUI에서 호출해 비주얼을 갱신한다.</summary>
    public void RefreshState()
    {
        if (MapManager.Instance == null) return;

        bool isVisited   = MapManager.Instance.IsNodeVisited(nodeIndex);
        bool isAvailable = MapManager.Instance.IsNodeAvailable(nodeIndex);
        bool isCurrent   = MapManager.Instance.CurrentNodeIndex == nodeIndex;

        // 오버레이 표시
        if (visitedOverlay != null)  visitedOverlay.SetActive(isVisited && !isCurrent);
        if (currentOverlay != null)  currentOverlay.SetActive(isCurrent);

        // 선택 가능한 노드만 버튼 활성화
        if (nodeButton != null)
            nodeButton.interactable = isAvailable;
    }

    private void OnButtonClicked()
    {
        onNodeClicked?.Invoke(nodeIndex);
    }

    // ─── 헬퍼 ────────────────────────────────────────────────────

    private Color GetNodeColor(MapNodeType type)
    {
        return type switch
        {
            MapNodeType.Combat => combatColor,
            MapNodeType.Elite  => eliteColor,
            MapNodeType.Boss   => bossColor,
            MapNodeType.Rest   => restColor,
            MapNodeType.Shop   => shopColor,
            _                  => Color.white,
        };
    }

    private string GetNodeTypeName(MapNodeType type)
    {
        return type switch
        {
            MapNodeType.Combat => "전투",
            MapNodeType.Elite  => "엘리트",
            MapNodeType.Boss   => "보스",
            MapNodeType.Rest   => "휴식",
            MapNodeType.Shop   => "상점",
            _                  => "?",
        };
    }
}
