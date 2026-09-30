using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 아군 전멸(파티 전원 무력화) 시 "스테이지 재시작 / 포기하기" 팝업을 표시한다.
///
/// 감지 방식:
///   Unit.OnAnyUnitDead를 구독 → 죽은 유닛이 플레이어 팀이면 한 프레임 뒤 확인.
///   한 프레임을 기다리는 이유: PartyManager.Unit_OnAnyUnitDead가 같은 이벤트를 받아
///   partyUnits → incapacitatedUnits로 이동시키는데, 이벤트 구독 순서에 따라
///   WipeScreenUI가 먼저 실행될 수 있기 때문이다.
///
/// Inspector 설정:
///   - panel: 전멸 팝업 루트 GameObject
///   - characterSelectUI: 포기 시 캐릭터 선택 화면을 표시하기 위한 참조
/// </summary>
public class WipeScreenUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private MainMenuUI mainMenuUI; // 포기 후 메인 메뉴로 복귀

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
        // 적·오브젝트 사망은 무시
        if (sender is Unit unit && unit.GetTeamType() != TeamType.Player) return;

        // PartyManager가 같은 이벤트를 받아 리스트를 업데이트하므로 한 프레임 뒤 확인
        StartCoroutine(CheckWipeNextFrame());
    }

    private IEnumerator CheckWipeNextFrame()
    {
        yield return null; // PartyManager의 Unit_OnAnyUnitDead 처리를 기다린다

        // partyUnits가 비었고 무력화된 유닛이 있으면 → 전멸
        // (HasIncapacitated() 조건: 단순히 파티가 비어있는 게임 시작 전 상태와 구별)
        if (PartyManager.Instance.GetPartyCount() == 0 && PartyManager.Instance.HasIncapacitated())
            Show();
    }

    private void Show() => panel?.SetActive(true);
    private void Hide() => panel?.SetActive(false);

    // ─── 버튼 이벤트 ──────────────────────────────────────────────

    /// <summary>
    /// [재시작 버튼]
    /// 마지막 세이브(노드 선택 시점, 즉 전투 진입 직전)를 복원하고 같은 스테이지를 처음부터 재시작한다.
    ///
    /// 흐름:
    ///   1. ApplyLoadedSave() → 파티 HP·스탯·스킬·골드를 전투 전 상태로 되돌린다.
    ///   2. 현재 맵의 currentNodeIndex로 해당 스테이지 StageData를 찾는다.
    ///   3. StageManager.LoadStage()로 해당 스테이지를 처음부터 로드한다.
    /// </summary>
    public void OnRestartClicked()
    {
        Hide();

        // 노드 선택 시점 세이브로 파티 상태 복원
        SaveSystem.Instance.ApplyLoadedSave();

        // 복원된 맵에서 마지막으로 선택한 노드의 StageData를 가져온다
        int nodeIdx = MapManager.Instance.CurrentNodeIndex;
        if (nodeIdx < 0 || MapManager.Instance.CurrentMapData == null) return;

        StageData stageData = MapManager.Instance.CurrentMapData.nodes[nodeIdx].stageData;
        if (stageData == null) return; // 전투 노드가 아닌 경우(이벤트·상점 등) 스킵

        StageManager.Instance.LoadStage(stageData);
    }

    /// <summary>
    /// [포기하기 버튼]
    /// 세이브 파일을 삭제하고 런 데이터를 초기화한 뒤 캐릭터 선택 화면으로 돌아간다.
    ///
    /// 씬은 리로드하지 않으므로 전장의 잔여 적 오브젝트는 화면에 남아있을 수 있다.
    /// 플레이어가 새 게임을 시작하면 StartGame() → LoadStage()에서 ClearEnemyUnits()가 호출되어 정리된다.
    /// </summary>
    public void OnQuitClicked()
    {
        Hide();

        // 세이브 파일 삭제 → 이어하기 버튼이 비활성화 상태로 표시됨
        SaveSystem.Instance.DeleteSave();

        // 런 관련 데이터 전부 초기화
        PartyManager.Instance.ClearParty();              // 유닛 GameObject 파괴
        PartySkillData.Instance.ResetAll();              // 습득 스킬 초기화
        GoldSystem.Instance.ResetGold();                 // 골드 초기화
        SkillEnhancementTokenManager.Instance.ResetAll(); // 토큰 초기화

        // 세이브가 삭제된 상태이므로 메인 메뉴의 이어하기 버튼은 자동으로 비활성화된다
        mainMenuUI?.Show();
    }
}
