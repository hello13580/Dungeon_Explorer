using System;
using System.Collections.Generic;

/// <summary>
/// 런(플레이 1회) 전체를 디스크에 기록하는 최상위 직렬화 컨테이너.
///
/// JsonUtility로 JSON 변환 후 Application.persistentDataPath/save.json에 저장된다.
/// JsonUtility는 Dictionary를 직렬화하지 못하므로, 스킬 습득 정보처럼
/// Key-Value 쌍이 필요한 데이터는 List<ClassSkillSaveData>로 변환해 저장한다.
/// </summary>
[Serializable]
public class SaveData
{
    /// <summary>현재 살아있는(전투 가능한) 파티원 목록.</summary>
    public List<UnitSaveData> partyUnits = new List<UnitSaveData>();

    /// <summary>전투 중 쓰러져 무력화된 파티원 목록. 휴식 노드에서 부활 가능.</summary>
    public List<UnitSaveData> incapacitatedUnits = new List<UnitSaveData>();

    /// <summary>현재 보유 골드.</summary>
    public int gold;

    /// <summary>스킬 강화 토큰 보유량.</summary>
    public int skillTokens;

    /// <summary>클래스별로 이번 런에서 배운 스킬 목록. PartySkillData의 Dictionary를 List로 변환.</summary>
    public List<ClassSkillSaveData> learnedSkills = new List<ClassSkillSaveData>();

    /// <summary>현재 진행 중인 맵의 노드 구조·진행 상태.</summary>
    public MapSaveData map = new MapSaveData();
}

/// <summary>
/// 파티원 한 명의 상태를 담는 데이터 클래스.
///
/// 복원 시 흐름:
///   1. prefabName으로 CharacterRoster에서 CharacterData를 찾는다.
///   2. CharacterData.unitPrefab으로 유닛 GameObject를 생성한다.
///   3. OverridePermanentStats()로 스탯 부스트를 덮어씌운다.
///   4. HealthSystem.SetHealth()로 HP를 복원한다.
/// </summary>
[Serializable]
public class UnitSaveData
{
    /// <summary>
    /// CharacterData.unitPrefab.name 값.
    /// 복원 시 CharacterRoster를 순회하며 이 이름과 일치하는 CharacterData를 찾는다.
    /// </summary>
    public string prefabName;

    /// <summary>저장 시점의 현재 HP. 스테이지 진입 시점에 저장하므로 전투 전 HP가 기록된다.</summary>
    public int currentHP;

    /// <summary>최대 HP. 프리팹 기본값이며, 현재 구현에서 스탯 부스트로 변하지 않는다.</summary>
    public int maxHP;

    /// <summary>공격력 (기본값 + 이번 런에서 획득한 영구 증가분).</summary>
    public int attackPower;

    /// <summary>방어력 (기본값 + 이번 런에서 획득한 영구 증가분).</summary>
    public int defensePower;

    /// <summary>
    /// 기준 이동 속도. 턴 시스템이 매 라운드 이 값으로 actionGauge를 올린다.
    /// AddPermanentSpeed()가 이 값도 증가시키므로 저장·복원이 필요하다.
    /// </summary>
    public float initialSpeed;

    /// <summary>현재 이동 속도. initialSpeed와 함께 AddPermanentSpeed()에 의해 변한다.</summary>
    public float currentSpeed;

    /// <summary>최대 행동 포인트. Token +1 보상을 선택하면 증가한다.</summary>
    public int maxActionPoint;
}

/// <summary>
/// 클래스 하나가 이번 런에서 배운 스킬 목록.
/// PartySkillData.learnedSkills(Dictionary)를 직렬화하기 위한 래퍼.
/// </summary>
[Serializable]
public class ClassSkillSaveData
{
    /// <summary>UnitSkillConfig.unitClassId 값. 클래스를 고유하게 식별한다.</summary>
    public string classId;

    /// <summary>이 클래스가 배운 스킬의 타입 이름 목록 (예: "IceOrbAction").</summary>
    public List<string> learnedActionTypeNames = new List<string>();
}

/// <summary>
/// 맵 노드 구조와 진행 상태를 저장하는 컨테이너.
///
/// MapGenerator는 랜덤 시드 없이 맵을 생성하므로 재실행 시 동일한 맵을 복원할 수 없다.
/// 따라서 생성된 노드 구조(타입·위치·연결·스테이지 이름)를 통째로 직렬화한다.
/// ScriptableObject(StageData, EventNodeData) 참조는 .name 문자열로 저장하고,
/// 복원 시 MapGenerationConfig에서 이름으로 다시 찾아온다.
/// </summary>
[Serializable]
public class MapSaveData
{
    /// <summary>맵의 모든 노드 데이터. 인덱스 순서가 곧 nodeId(node_0, node_1, ...)다.</summary>
    public List<NodeSaveData> nodes = new List<NodeSaveData>();

    /// <summary>게임 시작 시 선택 가능한 첫 노드들의 인덱스.</summary>
    public List<int> startNodeIndices = new List<int>();

    /// <summary>플레이어가 이미 방문한 노드 인덱스 집합.</summary>
    public List<int> visitedNodes = new List<int>();

    /// <summary>현재 선택 가능한 노드 인덱스 집합.</summary>
    public List<int> availableNodes = new List<int>();

    /// <summary>마지막으로 방문한(현재 위치한) 노드 인덱스. -1이면 아직 시작 전.</summary>
    public int currentNodeIndex = -1;
}

/// <summary>
/// 맵 노드 하나의 직렬화 데이터.
/// MapNodeData ScriptableObject를 직렬화 가능한 형태로 변환한 것.
/// </summary>
[Serializable]
public class NodeSaveData
{
    /// <summary>노드 타입 (Combat, Elite, Boss, Rest, Event, Shop, Start).</summary>
    public MapNodeType nodeType;

    /// <summary>MapUI에서 사용하는 정규화 X 좌표(-0.5 ~ 0.5). MapNodeData.position.x.</summary>
    public float posX;

    /// <summary>MapUI에서 사용하는 정규화 Y 좌표(-0.5 ~ 0.5). MapNodeData.position.y.</summary>
    public float posY;

    /// <summary>이 노드에서 이동 가능한 다음 노드들의 인덱스 배열.</summary>
    public int[] nextNodeIndices;

    /// <summary>
    /// 전투/엘리트/보스 노드의 StageData ScriptableObject 이름.
    /// 복원 시 MapGenerator.FindStageByName()으로 실제 레퍼런스를 찾는다.
    /// 해당 없으면 빈 문자열.
    /// </summary>
    public string stageDataName;

    /// <summary>
    /// 이벤트 노드의 EventNodeData ScriptableObject 이름.
    /// 복원 시 MapGenerator.FindEventByName()으로 실제 레퍼런스를 찾는다.
    /// 해당 없으면 빈 문자열.
    /// </summary>
    public string eventDataName;
}
