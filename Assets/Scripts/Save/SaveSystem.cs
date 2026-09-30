using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 런 상태를 JSON 파일로 저장·복원하는 DontDestroyOnLoad 싱글톤.
///
/// 저장 경로: Application.persistentDataPath/save.json
///   (Windows: %APPDATA%/../LocalLow/{company}/{product}/save.json)
///
/// 저장 타이밍은 SaveHookManager가 이벤트를 구독해 자동으로 처리한다.
///   - 맵 노드 선택 시 (전투 진입 직전 → 재시작 기준점)
///   - 스테이지 클리어 시 (HP 소모 후 상태 보존)
///   - 보상 선택 완료 시 (스킬·스탯 증가 후 상태 보존)
///
/// Inspector에서 반드시 설정해야 할 항목:
///   - characterRoster: 프리팹 이름으로 CharacterData를 찾을 때 사용
/// </summary>
public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }

    /// <summary>복원 시 prefabName → CharacterData 매핑에 사용. Inspector에서 할당 필수.</summary>
    [SerializeField] private CharacterRoster characterRoster;

    private const string SaveFileName = "save.json";

    /// <summary>플랫폼별 올바른 저장 경로를 반환한다.</summary>
    private string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── 파일 유무 / 삭제 ─────────────────────────────────────────

    /// <summary>세이브 파일이 존재하는지 확인. 이어하기 버튼 활성화 여부에 사용.</summary>
    public bool HasSave() => File.Exists(SavePath);

    /// <summary>
    /// 세이브 파일을 삭제한다.
    /// 뉴 게임 시작 시(StartGame), 런 포기 시(WipeScreenUI.OnQuitClicked)에 호출된다.
    /// </summary>
    public void DeleteSave()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
    }

    // ─── 저장 ─────────────────────────────────────────────────────

    /// <summary>
    /// 현재 게임 상태를 SaveData로 수집한 뒤 JSON으로 직렬화해 디스크에 쓴다.
    /// SaveHookManager에서 이벤트가 발생할 때 자동으로 호출된다.
    /// </summary>
    public void Save()
    {
        SaveData data = new SaveData();
        CollectPartyData(data);      // 유닛 HP·스탯
        CollectResourceData(data);   // 골드·토큰
        CollectSkillData(data);      // 습득 스킬
        CollectMapData(data);        // 맵 구조·진행 상태

        File.WriteAllText(SavePath, JsonUtility.ToJson(data, prettyPrint: true));
        Debug.Log($"[SaveSystem] 저장 완료: {SavePath}");
    }

    // --- 수집 헬퍼 ---

    /// <summary>PartyManager의 파티원·무력화 유닛을 UnitSaveData 리스트로 변환한다.</summary>
    private void CollectPartyData(SaveData data)
    {
        foreach (Unit u in PartyManager.Instance.GetPartyUnits())
            data.partyUnits.Add(BuildUnitData(u));
        foreach (Unit u in PartyManager.Instance.GetIncapacitatedUnits())
            data.incapacitatedUnits.Add(BuildUnitData(u));
    }

    /// <summary>
    /// 유닛 한 명의 상태를 UnitSaveData로 변환한다.
    /// 기본값(프리팹 inspector 값)이 아닌 현재 누적값을 저장하므로
    /// 복원 시 OverridePermanentStats()로 직접 덮어쓴다.
    /// </summary>
    private UnitSaveData BuildUnitData(Unit unit)
    {
        return new UnitSaveData
        {
            // PartyManager에 등록된 프리팹 이름으로 복원 시 CharacterData를 찾는다
            prefabName     = PartyManager.Instance.GetPrefabName(unit),
            currentHP      = unit.GetCurrentHealth(),
            maxHP          = unit.GetHealthSystem().GetMaxHealth(),
            // GetBaseAttackPower()는 AttackBuffSystem 버프를 제외한 순수 영구 스탯을 반환
            attackPower    = unit.GetBaseAttackPower(),
            defensePower   = unit.GetBaseDefensePower(),
            // AddPermanentSpeed()가 initialSpeed와 currentSpeed를 모두 올리므로 둘 다 저장
            initialSpeed   = unit.GetInitialSpeed(),
            currentSpeed   = unit.GetCurrentSpeed(),
            maxActionPoint = unit.GetMaxActionPoint(),
        };
    }

    /// <summary>GoldSystem과 SkillEnhancementTokenManager의 현재 값을 기록한다.</summary>
    private void CollectResourceData(SaveData data)
    {
        data.gold        = GoldSystem.Instance.GetGold();
        data.skillTokens = SkillEnhancementTokenManager.Instance.GetTokenCount();
    }

    /// <summary>
    /// PartySkillData.learnedSkills(Dictionary)를 직렬화 가능한 List로 변환한다.
    /// JsonUtility는 Dictionary를 직렬화하지 못하므로 ClassSkillSaveData 리스트를 사용한다.
    /// </summary>
    private void CollectSkillData(SaveData data)
    {
        foreach (var kvp in PartySkillData.Instance.GetAllLearnedSkills())
        {
            var entry = new ClassSkillSaveData { classId = kvp.Key };
            entry.learnedActionTypeNames.AddRange(kvp.Value);
            data.learnedSkills.Add(entry);
        }
    }

    /// <summary>
    /// MapManager의 현재 맵 데이터와 진행 상태를 직렬화한다.
    /// MapGenerator가 시드 없이 랜덤 생성하므로 노드 구조 전체를 저장해야 한다.
    /// ScriptableObject 레퍼런스는 .name 문자열로 대체한다.
    /// </summary>
    private void CollectMapData(SaveData data)
    {
        MapData mapData = MapManager.Instance.CurrentMapData;
        if (mapData == null) return; // 맵이 아직 생성되지 않은 상태면 스킵

        MapSaveData m = data.map;
        int currentNode = MapManager.Instance.CurrentNodeIndex;
        m.currentNodeIndex = currentNode;

        bool isCleared = MapManager.Instance.IsCurrentNodeCleared;

        if (!isCleared && currentNode >= 0)
        {
            // 아직 클리어되지 않은 노드에 진입한 채로 저장된 상태.
            // (노드 선택 직후 전투 중 종료 등)
            // currentNode를 visitedNodes에서 제외하고 availableNodes에 넣어
            // 컨티뉴 시 해당 스테이지를 처음부터 다시 진행하도록 한다.
            foreach (int idx in MapManager.Instance.GetVisitedNodes())
                if (idx != currentNode)
                    m.visitedNodes.Add(idx);
            m.availableNodes.Add(currentNode);
        }
        else
        {
            // 클리어된 상태(스테이지 클리어·보상 선택 완료 시점) → 그대로 저장
            m.visitedNodes.AddRange(MapManager.Instance.GetVisitedNodes());
            m.availableNodes.AddRange(MapManager.Instance.GetAvailableNodes());
        }

        m.startNodeIndices.AddRange(mapData.startNodeIndices);

        foreach (MapNodeData node in mapData.nodes)
        {
            m.nodes.Add(new NodeSaveData
            {
                nodeType        = node.nodeType,
                posX            = node.position.x,
                posY            = node.position.y,
                nextNodeIndices = node.nextNodeIndices,
                // ScriptableObject는 레퍼런스를 직렬화할 수 없으므로 이름만 저장
                stageDataName   = node.stageData  != null ? node.stageData.name  : "",
                eventDataName   = node.eventData  != null ? node.eventData.name  : "",
            });
        }
    }

    // ─── 복원 ─────────────────────────────────────────────────────

    /// <summary>
    /// 세이브 파일을 읽어 게임 상태 전체를 복원한다.
    /// CharacterSelectManager.ContinueGame()과 WipeScreenUI.OnRestartClicked()에서 호출된다.
    ///
    /// 복원 순서가 중요하다:
    ///   1. 파티 유닛 생성 (Awake 실행, Start는 SetActive(true) 전까지 지연)
    ///   2. 스킬 데이터 복원 → Start()의 ApplyLearnedSkills()가 이를 참조하므로 먼저 설정
    ///   3. 맵 복원 → MapUI가 OnMapStateChanged를 받아 화면을 갱신
    /// </summary>
    public void ApplyLoadedSave()
    {
        SaveData data = LoadRaw();
        if (data == null) { Debug.LogError("[SaveSystem] 세이브 파일 없음"); return; }

        ApplyPartyData(data);    // 유닛 생성 + 스탯·HP 복원
        ApplyResourceData(data); // 골드·토큰 복원
        ApplySkillData(data);    // 스킬 습득 기록 복원 (유닛 Start()보다 먼저)
        ApplyMapData(data);      // 맵 구조·진행 상태 복원
    }

    /// <summary>JSON 파일을 읽어 SaveData 객체로 역직렬화한다.</summary>
    private SaveData LoadRaw()
    {
        if (!HasSave()) return null;
        return JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
    }

    // --- 복원 헬퍼 ---

    /// <summary>
    /// 파티원·무력화 유닛을 생성하고 저장된 스탯·HP를 덮어쓴다.
    ///
    /// 무력화 유닛은 AddToParty로 partyUnits에 추가한 뒤 즉시 MoveToIncapacitated()로
    /// 이동시킨다. HP는 0으로 설정해 무력화 상태를 유지한다.
    /// </summary>
    private void ApplyPartyData(SaveData data)
    {
        PartyManager.Instance.ClearParty(); // 기존 유닛 전부 파괴

        foreach (UnitSaveData ud in data.partyUnits)
        {
            CharacterData cd = FindCharacter(ud.prefabName);
            if (cd == null) { Debug.LogWarning($"[SaveSystem] '{ud.prefabName}' 프리팹을 찾을 수 없음"); continue; }

            Unit unit = PartyManager.Instance.AddToParty(cd); // 생성 후 partyUnits 등록
            ApplyUnitStats(unit, ud);                         // 영구 스탯 덮어쓰기
            unit.GetHealthSystem().SetHealth(ud.currentHP);   // HP 복원
        }

        foreach (UnitSaveData ud in data.incapacitatedUnits)
        {
            CharacterData cd = FindCharacter(ud.prefabName);
            if (cd == null) continue;

            Unit unit = PartyManager.Instance.AddToParty(cd);
            ApplyUnitStats(unit, ud);
            unit.GetHealthSystem().SetHealth(0);       // 무력화 상태는 HP=0
            PartyManager.Instance.MoveToIncapacitated(unit); // incapacitatedUnits로 이동
        }
    }

    /// <summary>
    /// 저장된 영구 스탯을 유닛에 즉시 덮어쓴다.
    /// 프리팹의 inspector 기본값 위에 런 동안 획득한 부스트가 합산된 최종값이다.
    /// </summary>
    private void ApplyUnitStats(Unit unit, UnitSaveData ud)
    {
        unit.OverridePermanentStats(ud.attackPower, ud.defensePower,
                                    ud.initialSpeed, ud.currentSpeed, ud.maxActionPoint);
    }

    /// <summary>골드와 스킬 토큰을 초기화 후 저장값으로 설정한다.</summary>
    private void ApplyResourceData(SaveData data)
    {
        GoldSystem.Instance.ResetGold();
        GoldSystem.Instance.AddGold(data.gold);

        SkillEnhancementTokenManager.Instance.ResetAll();
        SkillEnhancementTokenManager.Instance.AddTokens(data.skillTokens);
    }

    /// <summary>
    /// List로 저장된 스킬 데이터를 PartySkillData의 내부 Dictionary로 복원한다.
    /// 유닛 Start()의 ApplyLearnedSkills()가 PartySkillData를 읽으므로
    /// 유닛이 활성화되기 전에 이 메서드를 먼저 호출해야 한다.
    /// </summary>
    private void ApplySkillData(SaveData data)
    {
        PartySkillData.Instance.ResetAll();
        foreach (ClassSkillSaveData entry in data.learnedSkills)
            foreach (string actionName in entry.learnedActionTypeNames)
                PartySkillData.Instance.LearnSkill(entry.classId, actionName);
    }

    /// <summary>
    /// 직렬화된 맵 데이터로 MapData ScriptableObject를 재구성하고 MapManager에 복원한다.
    /// RestoreFromSave()는 OnNodeVisited를 발생시키지 않으므로 자동 저장이 트리거되지 않는다.
    /// </summary>
    private void ApplyMapData(SaveData data)
    {
        MapSaveData m = data.map;
        MapData mapData = RebuildMapData(m);
        MapManager.Instance.RestoreFromSave(mapData,
            m.currentNodeIndex,
            new HashSet<int>(m.visitedNodes),
            new HashSet<int>(m.availableNodes));
    }

    /// <summary>
    /// NodeSaveData 목록으로 MapData ScriptableObject 인스턴스를 런타임에 재조립한다.
    /// 저장된 stageDataName/eventDataName 문자열로 MapGenerator에서 실제 레퍼런스를 찾는다.
    /// </summary>
    private MapData RebuildMapData(MapSaveData m)
    {
        MapData mapData = ScriptableObject.CreateInstance<MapData>();
        MapNodeData[] nodes = new MapNodeData[m.nodes.Count];

        for (int i = 0; i < m.nodes.Count; i++)
        {
            NodeSaveData ns = m.nodes[i];
            MapNodeData node = ScriptableObject.CreateInstance<MapNodeData>();
            node.nodeId          = $"node_{i}";
            node.nodeType        = ns.nodeType;
            node.position        = new UnityEngine.Vector2(ns.posX, ns.posY);
            node.nextNodeIndices = ns.nextNodeIndices;
            // 이름 문자열 → ScriptableObject 레퍼런스로 역변환
            node.stageData       = MapGenerator.Instance.FindStageByName(ns.stageDataName);
            node.eventData       = MapGenerator.Instance.FindEventByName(ns.eventDataName);
            nodes[i] = node;
        }

        mapData.nodes            = nodes;
        mapData.startNodeIndices = m.startNodeIndices.ToArray();
        return mapData;
    }

    // ─── 헬퍼 ──────────────────────────────────────────────────────

    /// <summary>
    /// CharacterRoster를 순회해 prefabName과 일치하는 CharacterData를 반환한다.
    /// 찾지 못하면 null을 반환하고 경고 로그는 호출측에서 출력한다.
    /// </summary>
    private CharacterData FindCharacter(string prefabName)
    {
        if (characterRoster == null || string.IsNullOrEmpty(prefabName)) return null;
        foreach (CharacterData cd in characterRoster.characters)
            if (cd.unitPrefab != null && cd.unitPrefab.name == prefabName)
                return cd;
        return null;
    }
}
