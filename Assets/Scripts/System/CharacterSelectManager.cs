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
    [SerializeField] private MapData mapData;   // 캐릭터 선택 완료 후 열 맵
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

        // 파티 초기화 후 선택한 캐릭터 등록
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

        // 스테이지를 직접 로드하지 않고 맵을 열어서 플레이어가 첫 노드를 선택하게 한다
        if (mapUI == null)
        {
            Debug.LogError("[CharacterSelectManager] MapUI가 설정되지 않았습니다.");
            return;
        }
        MapManager.Instance.InitializeMap(mapData);
        mapUI.OpenMapFromExternal();
    }
}
