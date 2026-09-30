using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터 선택 흐름을 관리한다.
/// 선택 완료 후 PartyManager에 등록하고 첫 스테이지를 로드한다.
/// </summary>
public class CharacterSelectManager : MonoBehaviour
{
    public static CharacterSelectManager Instance { get; private set; }

    [Header("설정")]
    [SerializeField] private int requiredSelectCount = 4;
    [SerializeField] private MapUI mapUI;        // 맵 패널 직접 참조

    private List<CharacterData> selectedCharacters = new List<CharacterData>();

    public static event EventHandler OnSelectionChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ─── 선택 로직 ────────────────────────────────────────────────

    /// <summary>카드 클릭 시 호출. 선택/해제 토글.</summary>
    public void ToggleCharacter(CharacterData characterData)
    {
        if (selectedCharacters.Contains(characterData))
        {
            selectedCharacters.Remove(characterData);
        }
        else
        {
            if (selectedCharacters.Count >= requiredSelectCount) return; // 최대 인원 초과
            selectedCharacters.Add(characterData);
        }

        OnSelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool IsSelected(CharacterData characterData) => selectedCharacters.Contains(characterData);

    public int GetSelectedCount() => selectedCharacters.Count;

    public int GetRequiredCount() => requiredSelectCount;

    public bool CanStart() => selectedCharacters.Count == requiredSelectCount;

    // ─── 게임 시작 ────────────────────────────────────────────────

    /// <summary>시작 버튼 클릭 시 호출.</summary>
    public void StartGame()
    {
        if (!CanStart()) return;

        // 이전 런 세이브 삭제 후 파티 초기화
        SaveSystem.Instance?.DeleteSave();
        PartyManager.Instance.ClearParty();
        foreach (CharacterData characterData in selectedCharacters)
        {
            if (characterData.unitPrefab == null)
            {
                Debug.LogWarning($"[CharacterSelectManager] {characterData.className}의 unitPrefab이 없습니다.");
                continue;
            }
            PartyManager.Instance.AddToParty(characterData);
        }

        // 맵 자동 생성 후 열기
        if (mapUI == null)
        {
            Debug.LogError("[CharacterSelectManager] MapUI가 설정되지 않았습니다.");
            return;
        }
        if (MapGenerator.Instance == null)
        {
            Debug.LogError("[CharacterSelectManager] MapGenerator가 씬에 없습니다.");
            return;
        }
        MapData generated = MapGenerator.Instance.Generate();
        mapUI.SetMapData(generated);
        MapManager.Instance.InitializeMap(generated);
        mapUI.OpenMapFromExternal();
    }

    /// <summary>
    /// 이어하기 버튼 클릭 시 호출. 세이브 파일에서 런 상태를 복원하고 맵 화면을 연다.
    ///
    /// 흐름:
    ///   1. ApplyLoadedSave() → 파티·골드·스킬·맵 상태 복원
    ///   2. SetMapData()      → MapUI가 복원된 MapData를 참조하도록 업데이트
    ///   3. OpenMapFromExternal() → 맵 패널 표시
    ///
    /// CharacterSelectUI.Hide()는 OnContinueButtonClicked()에서 처리한다.
    /// </summary>
    public void ContinueGame()
    {
        SaveSystem.Instance.ApplyLoadedSave();
        // RestoreFromSave() 이후 MapManager.CurrentMapData가 복원된 맵을 가리키므로
        // SetMapData()에 바로 전달해 MapUI가 올바른 맵을 렌더링하도록 한다
        mapUI.SetMapData(MapManager.Instance.CurrentMapData);
        mapUI.OpenMapFromExternal();
    }
}
