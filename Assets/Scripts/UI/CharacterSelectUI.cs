using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 선택 화면을 담당한다.
/// 메인 메뉴에서 "처음부터하기"를 누르면 ShowFromMainMenu()로 열린다.
/// Start()에서는 자동으로 표시하지 않는다 — MainMenuUI가 흐름을 제어한다.
///
/// Inspector 설정:
///   - panel         : 이 화면의 루트 GameObject
///   - startButton   : 선택 완료 후 활성화
///   - backButton    : (선택) 클릭 시 메인 메뉴로 복귀
///   - selectionCountText: "2 / 4" 형태로 현재 선택 인원 표시
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private CharacterRoster characterRoster;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Transform cardContainer;

    [Header("UI 요소")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button backButton; // 메인 메뉴로 돌아가기 (없으면 무시)
    [SerializeField] private TextMeshProUGUI selectionCountText; // "2 / 4" 형태

    [Header("패널")]
    [SerializeField] private GameObject panel;

    // 이 화면을 열었던 메인 메뉴의 Show()를 저장해 뒤로가기에 사용한다
    private Action onBackCallback;

    private void Start()
    {
        CharacterSelectManager.OnSelectionChanged += OnSelectionChanged;
        CreateCards();
        // MainMenuUI가 ShowFromMainMenu()로 열기 전까지 항상 숨긴다.
        // Editor에서 패널이 활성 상태여도 런타임에는 숨겨진다.
        Hide();
    }

    private void OnDestroy()
    {
        CharacterSelectManager.OnSelectionChanged -= OnSelectionChanged;
    }

    private void CreateCards()
    {
        if (characterRoster == null || cardPrefab == null || cardContainer == null) return;

        foreach (Transform child in cardContainer)
            Destroy(child.gameObject);

        foreach (CharacterData data in characterRoster.characters)
        {
            GameObject cardObj = Instantiate(cardPrefab, cardContainer);
            cardObj.GetComponent<CharacterCardUI>().Setup(data);
        }
    }

    private void OnSelectionChanged(object sender, System.EventArgs e) => RefreshUI();

    private void RefreshUI()
    {
        int selected = CharacterSelectManager.Instance.GetSelectedCount();
        int required = CharacterSelectManager.Instance.GetRequiredCount();

        if (selectionCountText != null)
            selectionCountText.text = $"{selected} / {required}";

        if (startButton != null)
            startButton.interactable = CharacterSelectManager.Instance.CanStart();
    }

    // ─── 버튼 이벤트 ──────────────────────────────────────────────

    public void OnStartButtonClicked()
    {
        CharacterSelectManager.Instance.StartGame();
        Hide();
    }

    /// <summary>뒤로가기 버튼 클릭 시 호출. 메인 메뉴로 복귀한다.</summary>
    public void OnBackButtonClicked()
    {
        Hide();
        onBackCallback?.Invoke();
    }

    // ─── 표시 제어 ────────────────────────────────────────────────

    /// <summary>
    /// 메인 메뉴의 "처음부터하기"에서 호출한다.
    /// onBack에 MainMenuUI.Show를 전달하면 뒤로가기 시 메인 메뉴가 복원된다.
    /// </summary>
    public void ShowFromMainMenu(Action onBack = null)
    {
        onBackCallback = onBack;
        RefreshUI();
        Show();
    }

    private void Show() => panel?.SetActive(true);
    private void Hide() => panel?.SetActive(false);
}
