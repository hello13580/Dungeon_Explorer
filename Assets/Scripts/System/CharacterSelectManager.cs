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
    [SerializeField] private StageData firstStage;

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
        if (firstStage == null)
        {
            Debug.LogError("[CharacterSelectManager] firstStage가 설정되지 않았습니다.");
            return;
        }

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

        // 첫 스테이지 로드
        StageManager.Instance.LoadStage(firstStage);
    }
}
