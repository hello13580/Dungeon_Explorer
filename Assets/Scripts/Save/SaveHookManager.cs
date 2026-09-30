using System;
using UnityEngine;

/// <summary>
/// 자동 저장 트리거를 한 곳에 모아 관리하는 컴포넌트.
///
/// 세이브 타이밍 3곳:
///   1. 맵 노드 선택 시 (MapManager.OnNodeVisited)
///      → 전투 진입 직전 상태를 저장. 전멸 시 이 시점으로 복원해 재시작한다.
///   2. 스테이지 클리어 시 (UnitManager.OnStageClear)
///      → 클리어 직후(보상 선택 전) HP·골드를 저장.
///   3. 보상 선택 완료 시 (SkillUnlockManager.OnSkillUnlockCompleted)
///      → 스킬·스탯 부스트가 적용된 최종 상태를 저장.
///
/// OnNodeVisited를 사용하는 이유:
///   MapManager.OnMapStateChanged는 InitializeMap(뉴 게임)과 RestoreFromSave(세이브 복원) 시에도
///   발생하므로, 실제 노드 선택 시에만 발생하는 별도 이벤트가 필요하다.
/// </summary>
public class SaveHookManager : MonoBehaviour
{
    private void OnEnable()
    {
        // 맵 노드를 선택했을 때 → 전투 진입 직전 상태 저장
        MapManager.OnNodeVisited                  += OnNodeVisited;
        // 스테이지 클리어(적 전멸) 시 → 보상 선택 전 상태 저장
        UnitManager.OnStageClear                  += OnStageClear;
        // 모든 클래스의 보상 선택이 완료됐을 때 → 최종 상태 저장
        SkillUnlockManager.OnSkillUnlockCompleted += OnSaveRequested;
    }

    private void OnDisable()
    {
        MapManager.OnNodeVisited                  -= OnNodeVisited;
        UnitManager.OnStageClear                  -= OnStageClear;
        SkillUnlockManager.OnSkillUnlockCompleted -= OnSaveRequested;
    }

    /// <summary>
    /// 노드 선택 시 저장. 이 시점의 저장은 IsCurrentNodeCleared=false이므로
    /// CollectMapData()가 현재 노드를 visitedNodes에서 제외하고 availableNodes로 둔다.
    /// → 컨티뉴 시 해당 스테이지를 처음부터 다시 진행하도록 한다.
    /// </summary>
    private void OnNodeVisited(object sender, EventArgs e)
    {
        if (SaveSystem.Instance != null)
            SaveSystem.Instance.Save();
    }

    /// <summary>
    /// 스테이지 클리어 시 저장. Save() 전에 MarkCurrentNodeCleared()를 호출해
    /// CollectMapData()가 현재 노드를 visitedNodes에 포함시키도록 한다.
    /// </summary>
    private void OnStageClear(object sender, EventArgs e)
    {
        MapManager.Instance?.MarkCurrentNodeCleared();
        if (SaveSystem.Instance != null)
            SaveSystem.Instance.Save();
    }

    private void OnSaveRequested(object sender, EventArgs e)
    {
        if (SaveSystem.Instance != null)
            SaveSystem.Instance.Save();
    }
}
