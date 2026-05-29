using System;
using UnityEngine;

[CreateAssetMenu(fileName = "StageData", menuName = "Stage/StageData")]
public class StageData : ScriptableObject
{
    [Header("스테이지 정보")]
    public string stageName;

    [Header("맵")]
    public GameObject mapPrefab;
    public int floorAmount = 1;

    [Header("적 유닛 스폰")]
    public EnemySpawnInfo[] enemySpawnInfos;

    [Header("아군 유닛 스폰")]
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
