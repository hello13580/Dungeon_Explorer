using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아군 파티원을 스테이지 간 유지하는 싱글턴.
/// DontDestroyOnLoad로 유지된다.
/// </summary>
public class PartyManager : MonoBehaviour
{
    public static PartyManager Instance { get; private set; }

    private List<Unit> partyUnits = new List<Unit>();
    private List<Unit> incapacitatedUnits = new List<Unit>();

    /// <summary>
    /// 유닛 인스턴스 → 원본 프리팹 이름 매핑.
    /// SaveSystem이 UnitSaveData.prefabName을 기록할 때 사용한다.
    /// AddToParty()에서 채워지고, ClearParty()에서 초기화된다.
    /// </summary>
    private Dictionary<Unit, string> unitToPrefabName = new Dictionary<Unit, string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        Unit.OnAnyUnitDead += Unit_OnAnyUnitDead;
    }

    private void OnDisable()
    {
        Unit.OnAnyUnitDead -= Unit_OnAnyUnitDead;
    }

    private void Unit_OnAnyUnitDead(object sender, EventArgs e)
    {
        // 전투 중 쓰러진 파티원은 무력화 리스트로 이동
        if (sender is Unit unit && partyUnits.Contains(unit))
        {
            partyUnits.Remove(unit);
            incapacitatedUnits.Add(unit);
        }
    }

    // ─── 파티 구성 ────────────────────────────────────────────────

    /// <summary>
    /// 캐릭터 선택 화면에서 호출.
    /// 유닛 프리팹으로 파티원을 생성하고 DontDestroyOnLoad로 유지한다.
    /// </summary>
    public Unit AddToParty(GameObject unitPrefab)
    {
        GameObject unitObj = Instantiate(unitPrefab);
        DontDestroyOnLoad(unitObj);
        unitObj.SetActive(false); // 스테이지 로드 전까지 비활성

        Unit unit = unitObj.GetComponent<Unit>();
        partyUnits.Add(unit);
        unitToPrefabName[unit] = unitPrefab.name;
        return unit;
    }

    /// <summary>
    /// CharacterData를 받아 파티원을 생성한다.
    /// CharacterSelectManager와 SaveSystem 복원 양쪽에서 사용한다.
    /// 내부적으로 AddToParty(GameObject)를 호출해 unitToPrefabName도 함께 등록된다.
    /// </summary>
    public Unit AddToParty(CharacterData characterData)
    {
        if (characterData == null || characterData.unitPrefab == null) return null;
        return AddToParty(characterData.unitPrefab);
    }

    /// <summary>
    /// 유닛 인스턴스에 대응하는 원본 프리팹 이름을 반환한다.
    /// SaveSystem.BuildUnitData()에서 UnitSaveData.prefabName을 채울 때 사용.
    /// 등록 기록이 없으면 GameObject 이름에서 "(Clone)" 접미어를 제거해 반환한다.
    /// </summary>
    public string GetPrefabName(Unit unit)
    {
        if (unitToPrefabName.TryGetValue(unit, out string name)) return name;
        return unit.gameObject.name.Replace("(Clone)", "").Trim();
    }

    /// <summary>
    /// 파티원을 무력화 리스트로 즉시 이동시킨다.
    /// SaveSystem이 세이브 복원 시 무력화 유닛을 재구성할 때 호출한다.
    /// 전투 중 사망은 Unit_OnAnyUnitDead에서 자동으로 처리되므로 이 메서드가 필요 없다.
    /// </summary>
    public void MoveToIncapacitated(Unit unit)
    {
        if (!partyUnits.Contains(unit)) return;
        partyUnits.Remove(unit);
        incapacitatedUnits.Add(unit);
    }

    /// <summary>
    /// 뉴 게임 시 호출. 파티원을 전부 파괴하고 초기화한다.
    /// </summary>
    public void ClearParty()
    {
        foreach (Unit unit in partyUnits)
            if (unit != null) Destroy(unit.gameObject);
        partyUnits.Clear();
        foreach (Unit unit in incapacitatedUnits)
            if (unit != null) Destroy(unit.gameObject);
        incapacitatedUnits.Clear();
        unitToPrefabName.Clear();
    }

    /// <summary>무력화된 유닛을 부활시켜 파티에 복귀시킨다. 휴식 노드에서 호출.</summary>
    public bool ReviveUnit(Unit unit, int healAmount = 1)
    {
        if (!incapacitatedUnits.Contains(unit)) return false;
        incapacitatedUnits.Remove(unit);
        partyUnits.Add(unit);
        unit.GetComponent<HealthSystem>()?.Heal(healAmount);
        return true;
    }

    /// <summary>무력화된 유닛 전체를 부활시킨다.</summary>
    public void ReviveAll(int healAmount = 1)
    {
        foreach (Unit unit in new List<Unit>(incapacitatedUnits))
            ReviveUnit(unit, healAmount);
    }

    // ─── 스테이지 전환 ────────────────────────────────────────────

    /// <summary>
    /// StageManager가 새 스테이지를 로드할 때 호출.
    /// 파티원을 스폰 포인트에 배치하고 LevelGrid / UnitManager에 재등록한다.
    /// </summary>
    public void PositionPartyAtSpawnPoints(Transform[] spawnPoints)
    {
        for (int i = 0; i < partyUnits.Count && i < spawnPoints.Length; i++)
        {
            partyUnits[i].gameObject.SetActive(true);
            partyUnits[i].transform.rotation = spawnPoints[i].rotation;
            partyUnits[i].RegisterForNewStage(spawnPoints[i].position);
        }
    }

    // ─── 조회 ─────────────────────────────────────────────────────

    public List<Unit> GetPartyUnits() => partyUnits;
    public List<Unit> GetIncapacitatedUnits() => incapacitatedUnits;
    public int GetPartyCount() => partyUnits.Count;
    public bool HasParty() => partyUnits.Count > 0;
    public bool HasIncapacitated() => incapacitatedUnits.Count > 0;
}
