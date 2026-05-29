using UnityEngine;

/// <summary>
/// 맵 프리팹의 루트 오브젝트에 붙이는 스크립트.
/// 스폰 포인트와 패스파인딩 링크 컨테이너 정보를 StageManager에 전달한다.
/// </summary>
public class MapSetup : MonoBehaviour
{
    [Header("아군 스폰 포인트")]
    public Transform[] playerSpawnPoints;

    [Header("적 스폰 포인트")]
    public Transform[] enemySpawnPoints;

    [Header("패스파인딩 링크 컨테이너")]
    [Tooltip("계단·링크 오브젝트들의 부모 Transform. 없으면 PathFinding 링크 없이 Setup됨.")]
    public Transform pathfindingLinkContainer;
}
