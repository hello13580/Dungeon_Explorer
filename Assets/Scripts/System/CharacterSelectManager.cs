using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터 선택 흐름을 관리한다.
/// 선택 완료 후 PartyManager에 파티원을 등록하고 맵을 열어 첫 노드 선택을 유도한다.
/// </summary>
public class CharacterSelectManager : MonoBehaviour
{
    public static CharacterSelectManager Instance { get; private set; }

    [Header("설정")]
    [SerializeField] private int requiredSelectCount = 4;

    [Header("맵 연결")]
    [SerializeField] private MapData mapData; // 게임 시작 시 초기화할 맵 데이터
    [SerializeField] private MapUI mapUI;     // 맵 패널. 캐릭터 선택 완료 후 직접 열어준다.

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

        // 파티 초기화 후 선택한 캐릭터를 PartyManager에 등록
        // 이후 스테이지 로드 시 PartyManager.PositionPartyAtSpawnPoints()가 유닛을 배치한다
        PartyManager.Instance.ClearParty();
        foreach (CharacterData characterData in selectedCharacters)
        {
            if (characterData.unitPrefab == null)
            {
                Debug.LogWarning($"[CharacterSelectManager] {characterData.className}의 unitPrefab이 없습니다.");
                continue;
            }
            PartyManager.Instance.AddToParty(characterData.unitPrefab);
        }

        if (mapUI == null)
        {
            Debug.LogError("[CharacterSelectManager] MapUI가 설정되지 않았습니다. 인스펙터에서 연결해 주세요.");
            return;
        }

        // 스테이지를 직접 로드하지 않고 맵을 먼저 열어 플레이어가 첫 노드를 선택하게 한다.
        // 이전에는 firstStage를 바로 로드했는데, 그러면 맵이 나중에 열릴 때
        // startNode(Combat1)가 다시 선택 가능해져서 스테이지 1이 반복되는 버그가 있었다.
        MapManager.Instance.InitializeMap(mapData);
        mapUI.OpenMapFromExternal();
    }
}
