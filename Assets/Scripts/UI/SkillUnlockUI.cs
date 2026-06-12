using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스킬 습득 선택 UI 패널.
/// SkillUnlockManager 이벤트를 구독해 스테이지 클리어 후 자동으로 표시된다.
/// - 카드 클릭 → 확정 패널(confirmPanel) 표시 + 선택 비주얼
/// - 확정 버튼 → 스킬 습득 후 확정 패널 숨김 (메인 패널 유지)
/// - 취소 버튼 → 선택 해제, 확정 패널 숨김
/// - 컨티뉴 버튼 → 메인 패널 닫기 (스킬 선택 여부 무관)
/// </summary>
public class SkillUnlockUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform cardContainer;
    [SerializeField] private GameObject skillCardPrefab;
    [SerializeField] private GameObject noSkillsMessage;  // 습득 가능한 스킬이 없을 때 표시

    [Header("확정 패널 (카드 선택 시에만 표시)")]
    [SerializeField] private GameObject confirmPanel;     // 확정·취소 버튼을 묶은 부모 오브젝트
    [SerializeField] private Button confirmButton;        // 선택한 스킬 확정 버튼
    [SerializeField] private Button cancelButton;         // 선택 취소 버튼

    [SerializeField] private Button continueButton;       // 패널 닫기 버튼 (항상 표시)

    private SkillUnlockManager.SkillUnlockOption selectedOption;
    private List<SkillCardUI> spawnedCards = new List<SkillCardUI>();

    private void Start()
    {
        panel.SetActive(false);
        SkillUnlockManager.OnSkillUnlockStarted += OnSkillUnlockStarted;
        SkillUnlockManager.OnSkillUnlockCompleted += OnSkillUnlockCompleted;
    }

    private void OnDestroy()
    {
        SkillUnlockManager.OnSkillUnlockStarted -= OnSkillUnlockStarted;
        SkillUnlockManager.OnSkillUnlockCompleted -= OnSkillUnlockCompleted;
    }

    private void OnSkillUnlockStarted(object sender, List<SkillUnlockManager.SkillUnlockOption> options)
    {
        // 기존 카드 및 상태 초기화
        foreach (Transform child in cardContainer)
            Destroy(child.gameObject);
        spawnedCards.Clear();
        selectedOption = null;

        bool hasOptions = options != null && options.Count > 0;

        // 선택지 카드 생성
        if (hasOptions)
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
            noSkillsMessage.SetActive(!hasOptions);

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

    /// <summary>확정 버튼 클릭 시 호출. 선택한 스킬을 습득하지만 패널은 유지.</summary>
    public void OnConfirmButtonClicked()
    {
        if (selectedOption == null) return;

        SkillUnlockManager.Instance.ConfirmUnlock(selectedOption);

        // 확정 후 선택 상태 초기화 (중복 습득 방지)
        ClearSelection();
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

    /// <summary>컨티뉴 버튼 클릭 시 호출. 스킬 선택 여부 무관하게 패널 닫기.</summary>
    public void OnContinueButtonClicked()
    {
        SkillUnlockManager.Instance.SkipUnlock();
    }

    private void OnSkillUnlockCompleted(object sender, System.EventArgs e)
    {
        panel.SetActive(false);
    }
}
