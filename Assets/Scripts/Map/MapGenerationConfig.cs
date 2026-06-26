using System;
using UnityEngine;

/// <summary>
/// 맵 자동 생성에 필요한 설정값을 담는 ScriptableObject.
/// Create > TBRPG/Map/MapGenerationConfig 로 생성한다.
/// </summary>
[CreateAssetMenu(fileName = "MapGenerationConfig", menuName = "TBRPG/Map/MapGenerationConfig")]
public class MapGenerationConfig : ScriptableObject
{
    [Header("노드 수량 (시작·보스 제외)")]
    public int combatCount = 8;
    public int eliteCount  = 3;
    public int eventCount  = 5;
    public int shopCount   = 2;
    public int restCount   = 2;

    [Header("레이어별 스테이지 풀 (미들 레이어 10개 고정)")]
    [Tooltip("layers[0] = 미들 레이어 1, layers[9] = 미들 레이어 10.\n" +
             "각 레이어에 진입할 수 있는 전투·엘리트 스테이지를 지정한다.")]
    public LayerStageConfig[] layers = new LayerStageConfig[10];

    [Header("공용 데이터")]
    [Tooltip("보스 스테이지 (항상 마지막 노드에 배정)")]
    public StageData bossStage;
    [Tooltip("이벤트 노드에 랜덤 배정될 EventNodeData 목록 (레이어 무관)")]
    public EventNodeData[] eventDatas;

    [Header("맵 레이아웃")]
    [Tooltip("맵 UI 캔버스 너비 (픽셀)")]
    public float canvasWidth  = 820f;
    [Tooltip("맵 UI 캔버스 높이 (픽셀)")]
    public float canvasHeight = 420f;
    [Tooltip("레이어당 최대 노드 수")]
    [Range(1, 5)]
    public int maxNodesPerLayer = 4;
    [Tooltip("노드 간격 배율. 1이면 화면에 딱 맞게, 클수록 넓게 펼쳐진다. (패닝으로 탐색)")]
    [Range(1f, 4f)]
    public float spacingScale = 1.5f;

    [Header("제약")]
    [Tooltip("상점과 휴식처 사이 최소 홉 거리")]
    public int minShopRestDistance = 2;

    public const int MiddleLayerCount = 10;

    public int TotalMiddleNodeCount =>
        combatCount + eliteCount + eventCount + shopCount + restCount;
}

/// <summary>미들 레이어 하나에서 등장 가능한 스테이지 풀.</summary>
[Serializable]
public class LayerStageConfig
{
    [Tooltip("이 레이어의 일반 전투 스테이지 목록")]
    public StageData[] combatStages;
    [Tooltip("이 레이어의 엘리트 스테이지 목록")]
    public StageData[] eliteStages;
}
