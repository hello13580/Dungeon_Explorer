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
        return unit;
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
