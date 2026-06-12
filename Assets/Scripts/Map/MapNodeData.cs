using UnityEngine;

/// <summary>
/// 맵 위의 노드 하나를 정의하는 ScriptableObject.
/// MapData의 nodes 배열에 포함되어 맵 전체 구조를 구성한다.
/// </summary>
[CreateAssetMenu(fileName = "MapNodeData", menuName = "TBRPG/Map/MapNodeData")]
public class MapNodeData : ScriptableObject
{
    [Tooltip("노드의 고유 식별자. MapManager가 방문 기록에 사용한다.")]
    public string nodeId;

    [Tooltip("노드 타입 (전투/엘리트/보스/휴식/상점 등)")]
    public MapNodeType nodeType;

    [Tooltip("이 노드에 진입했을 때 로드할 스테이지. Rest·Shop처럼 전투가 없으면 null.")]
    public StageData stageData;

    [Tooltip("맵 UI에서 이 노드를 배치할 위치 (앵커 기준 픽셀 좌표)")]
    public Vector2 position;

    [Tooltip("이 노드에서 이동할 수 있는 다음 노드들. 인덱스는 MapData.nodes 배열 기준.")]
    public int[] nextNodeIndices;
}
