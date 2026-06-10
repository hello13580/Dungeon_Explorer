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
        // 전투 중 사망한 파티원은 파티에서 제거 (퍼머데스)
        if (sender is Unit unit && partyUnits.Contains(unit))
            partyUnits.Remove(unit);
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
            partyUnits[i].RegisterForNewStage(spawnPoints[i].position);
        }
    }

    // ─── 조회 ─────────────────────────────────────────────────────

    public List<Unit> GetPartyUnits() => partyUnits;
    public int GetPartyCount() => partyUnits.Count;
    public bool HasParty() => partyUnits.Count > 0;
}
