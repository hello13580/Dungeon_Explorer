using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스킬 습득 선택 UI 패널.
/// SkillUnlockManager 이벤트를 구독해 스테이지 클리어 후 자동으로 표시된다.
/// 직업마다 순서대로 한 번씩 보상 화면이 열린다.
/// - 카드 클릭 → 확정 패널(confirmPanel) 표시 + 선택 비주얼
/// - 확정 버튼 → 스킬 습득 후 확정 패널 숨김 (메인 패널 유지)
/// - 취소 버튼 → 선택 해제, 확정 패널 숨김
/// - 컨티뉴 버튼 → 현재 직업 보상 종료 후 다음 직업 보상으로 이동 (또는 패널 닫기)
/// </summary>
public class SkillUnlockUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform cardContainer;
    [SerializeField] private GameObject skillCardPrefab;
    [SerializeField] private GameObject noSkillsMessage;  // 습득 가능한 스킬이 없을 때 표시

    [Header("현재 직업 표시 텍스트")]
    [SerializeField] private TextMeshProUGUI classNameText; // "전사의 보상" 처럼 현재 직업명을 표시

    [Header("확정 패널 (카드 선택 시에만 표시)")]
    [SerializeField] private GameObject confirmPanel;     // 확정·취소 버튼을 묶은 부모 오브젝트
    [SerializeField] private Button confirmButton;        // 선택한 스킬 확정 버튼
    [SerializeField] private Button cancelButton;         // 선택 취소 버튼

    [SerializeField] private Button continueButton;       // 현재 직업 건너뛰기 / 모두 완료 후 닫기 버튼

    [Header("모든 보상 완료 시 표시할 오브젝트")]
    [SerializeField] private GameObject completedMessage; // "보상 선택 완료" 안내 텍스트 등

    private SkillUnlockManager.SkillUnlockOption selectedOption;
    private List<SkillCardUI> spawnedCards = new List<SkillCardUI>();
    private bool isAllCompleted = false;  // 모든 직업 보상이 끝났는지 여부
    private bool isSessionActive = false; // 현재 보상 세션이 진행 중인지 — 완료 이벤트 중복 처리 방지

    private void Start()
    {
        panel.SetActive(false);
        SkillUnlockManager.OnSkillUnlockStarted += OnSkillUnlockStarted;
        SkillUnlockManager.OnSkillUnlockCompleted += OnSkillUnlockCompleted;
        // 새 스테이지 로드가 시작되면 보상 화면을 즉시 닫는다
        StageManager.OnStageLoadingStarted += StageManager_OnStageLoaded;
    }

    private void OnDestroy()
    {
        SkillUnlockManager.OnSkillUnlockStarted -= OnSkillUnlockStarted;
        SkillUnlockManager.OnSkillUnlockCompleted -= OnSkillUnlockCompleted;
        StageManager.OnStageLoadingStarted -= StageManager_OnStageLoaded;
    }

    /// <summary>새 스테이지 로드 완료 시 보상 패널을 강제로 닫는다. 이전 스테이지 보상 화면이 남아있는 경우를 처리.</summary>
    private void StageManager_OnStageLoaded(object sender, System.EventArgs e)
    {
        isSessionActive = false;
        isAllCompleted = false;
        panel.SetActive(false);
    }

    private void OnSkillUnlockStarted(object sender, List<SkillUnlockManager.SkillUnlockOption> options)
    {
        isAllCompleted = false;
        isSessionActive = true;

        // 기존 카드 및 상태 초기화
        foreach (Transform child in cardContainer)
            Destroy(child.gameObject);
        spawnedCards.Clear();
        selectedOption = null;

        // 완료 메시지 숨기기
        if (completedMessage != null) completedMessage.SetActive(false);

        // 현재 직업명 텍스트 갱신 — 큐에서 꺼낸 단일 옵션의 unitClassId를 표시한다
        if (classNameText != null && options != null && options.Count > 0)
            classNameText.text = $"{options[0].unitClassId}의 보상";

        // options[0].skillDef가 null이면 이 직업은 배울 스킬이 없다
        bool hasSkill = options != null && options.Count > 0 && options[0].skillDef != null;

        // 선택지 카드 생성 (스킬이 있을 때만)
        if (hasSkill)
        {
            foreach (var option in options)
            {
                GameObject cardObj = Instantiate(skillCardPrefab, cardContainer);
                SkillCardUI card = cardObj.GetComponent<SkillCardUI>();
                // 카드 클릭 시 이 UI에 선택 알림을 보내도록 콜백 전달
                card.Setup(option, OnCardSelected);
                spawnedCards.Add(card);
            }
        }

        // 스킬이 없을 때 안내 메시지 표시
        if (noSkillsMessage != null)
            noSkillsMessage.SetActive(!hasSkill);

        // 확정 패널은 카드를 선택했을 때만 표시 — 패널 열릴 때는 항상 숨김
        if (confirmPanel != null)
            confirmPanel.SetActive(false);

        panel.SetActive(true);
    }

    /// <summary>카드 클릭 시 SkillCardUI에서 호출.</summary>
    private void OnCardSelected(SkillUnlockManager.SkillUnlockOption option)
    {
        // 이미 선택된 카드를 다시 클릭하면 선택 해제
        selectedOption = (selectedOption == option) ? null : option;

        // 모든 카드의 선택 비주얼 갱신
        foreach (SkillCardUI card in spawnedCards)
            card.SetSelected(card.GetOption() == selectedOption);

        // 카드가 선택되면 확정 패널 표시, 해제되면 숨김
        if (confirmPanel != null)
            confirmPanel.SetActive(selectedOption != null);
    }

    // ─── 버튼 이벤트 ──────────────────────────────────────────────

    /// <summary>확정 버튼 클릭 시 호출. 스킬을 습득하고 즉시 다음 직업 보상으로 이동한다.</summary>
    public void OnConfirmButtonClicked()
    {
        if (selectedOption == null) return;

        SkillUnlockManager.Instance.ConfirmUnlock(selectedOption);
        // 확정 즉시 다음 직업으로 — OnSkillUnlockStarted 또는 OnSkillUnlockCompleted가 이어서 호출됨
        SkillUnlockManager.Instance.SkipUnlock();
    }

    /// <summary>취소 버튼 클릭 시 호출. 카드 선택을 해제하고 다시 고를 수 있는 상태로 돌아간다.</summary>
    public void OnCancelButtonClicked()
    {
        ClearSelection();
    }

    /// <summary>선택 상태를 초기화한다. 확정·취소 버튼 모두 비활성, 카드 선택 비주얼 해제.</summary>
    private void ClearSelection()
    {
        selectedOption = null;

        // 모든 카드의 선택 비주얼 해제
        foreach (SkillCardUI card in spawnedCards)
            card.SetSelected(false);

        // 선택이 해제됐으니 확정 패널 숨김
        if (confirmPanel != null)
            confirmPanel.SetActive(false);
    }

    /// <summary>
    /// 컨티뉴 버튼 클릭 시 호출.
    /// 보상 진행 중: 현재 직업 건너뛰고 다음 직업으로 이동.
    /// 모두 완료 후: 패널을 닫는다.
    /// </summary>
    public void OnContinueButtonClicked()
    {
        if (isAllCompleted)
        {
            isSessionActive = false;
            panel.SetActive(false);
        }
        else
        {
            SkillUnlockManager.Instance.SkipUnlock();
        }
    }

    private void OnSkillUnlockCompleted(object sender, System.EventArgs e)
    {
        // 이 UI가 보상 세션을 진행 중일 때만 처리 — 이전 세션 잔재나 spurious 이벤트 무시
        if (!isSessionActive) return;
        isSessionActive = false;

        isAllCompleted = true;

        // 카드와 직업명, 확정 패널을 모두 숨기고 컨티뉴 버튼만 남긴다
        foreach (Transform child in cardContainer)
            Destroy(child.gameObject);
        spawnedCards.Clear();

        if (classNameText != null) classNameText.text = "";
        if (noSkillsMessage != null) noSkillsMessage.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (completedMessage != null) completedMessage.SetActive(true);
    }
}
