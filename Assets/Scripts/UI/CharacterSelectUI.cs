using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 선택 화면 전체를 담당한다.
/// 게임 시작 시 활성화되며, 시작 버튼 클릭 후 비활성화된다.
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private CharacterRoster characterRoster;
    [SerializeField] private GameObject cardPrefab;
    [SerializeField] private Transform cardContainer;

    [Header("UI 요소")]
    [SerializeField] private Button startButton;
    [SerializeField] private TextMeshProUGUI selectionCountText; // "2 / 4" 형태

    [Header("패널")]
    [SerializeField] private GameObject panel;

    private void Start()
    {
        CharacterSelectManager.OnSelectionChanged += OnSelectionChanged;

        CreateCards();
        RefreshUI();
        Show();
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

    private void OnSelectionChanged(object sender, System.EventArgs e)
    {
        RefreshUI();
    }

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

    // ─── 표시 제어 ────────────────────────────────────────────────

    private void Show() => panel.SetActive(true);
    private void Hide() => panel.SetActive(false);
}
