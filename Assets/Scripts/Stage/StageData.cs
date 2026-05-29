using System;
using UnityEngine;

// Project 우클릭 → Create > Stage > StageData 로 에셋 생성
[CreateAssetMenu(fileName = "StageData", menuName = "Stage/StageData")]
public class StageData : ScriptableObject
{
    [Header("스테이지 정보")]
    public string stageName;

    [Header("맵")]
    // 인스턴시에이트할 맵 프리팹 (루트에 MapSetup 컴포넌트 필수)
    public GameObject mapPrefab;
    // 맵에 사용하는 층 수 — LevelGrid·PathFinding 초기화 시 사용
    public int floorAmount = 1;
    // 맵 프리팹이 월드의 어느 좌표에 생성될지 (보통 Vector3.zero)
    public Vector3 mapSpawnPosition = Vector3.zero;

    [Header("카메라")]
    // 스테이지 시작 시 CameraController 오브젝트가 이동할 월드 위치
    // 씬에서 CameraController를 원하는 위치에 놓고 Inspector Position 값을 복사해서 입력
    [Tooltip("스테이지 시작 시 CameraController의 월드 위치")]
    public Vector3 cameraStartPosition;
    // 스테이지 시작 시 CameraController의 방향 (오일러각)
    // 씬에서 CameraController를 원하는 방향으로 돌리고 Inspector Rotation 값을 복사해서 입력
    [Tooltip("스테이지 시작 시 CameraController의 월드 로테이션 (오일러각)")]
    public Vector3 cameraStartRotation;
    // 카메라 이동 가능 범위 (XZ 평면 기준 최솟값·최댓값)
    // X = 월드 X축 범위, Y = 월드 Z축 범위
    [Tooltip("카메라가 이동할 수 있는 XZ 범위의 최솟값 (X=월드X, Y=월드Z)")]
    public Vector2 cameraBoundsMin;
    [Tooltip("카메라가 이동할 수 있는 XZ 범위의 최댓값 (X=월드X, Y=월드Z)")]
    public Vector2 cameraBoundsMax;

    [Header("라이팅")]
    // 씬의 Directional Light Intensity 값 — 낮/밤/실내 분위기 연출에 사용
    [Tooltip("Directional Light의 Intensity 값")]
    public float directionalLightIntensity = 1f;

    [Header("적 유닛 스폰")]
    // 각 항목: 어떤 프리팹을 MapSetup.enemySpawnPoints의 몇 번 위치에 스폰할지 지정
    public EnemySpawnInfo[] enemySpawnInfos;

    [Header("아군 유닛 스폰")]
    // 각 항목: 어떤 프리팹을 MapSetup.playerSpawnPoints의 몇 번 위치에 스폰할지 지정
    public PlayerSpawnInfo[] playerSpawnInfos;
}

[Serializable]
public class EnemySpawnInfo
{
    public GameObject unitPrefab;
    [Tooltip("MapSetup.enemySpawnPoints 배열의 인덱스")]
    public int spawnPointIndex;
}

[Serializable]
public class PlayerSpawnInfo
{
    public GameObject unitPrefab;
    [Tooltip("MapSetup.playerSpawnPoints 배열의 인덱스")]
    public int spawnPointIndex;
}
