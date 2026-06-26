using UnityEngine;

/// <summary>
/// 맵 전체 구조를 정의하는 ScriptableObject.
/// 노드 목록과 시작 노드 인덱스를 가진다.
/// </summary>
public class MapData : ScriptableObject
{
    [Tooltip("이 맵에 포함된 모든 노드 목록. 인덱스가 각 노드의 ID로 사용된다.")]
    public MapNodeData[] nodes;

    [Tooltip("게임 시작 시 처음으로 선택 가능한 노드 인덱스들 (보통 0번 하나 또는 여러 갈래 시작).")]
    public int[] startNodeIndices;
}
