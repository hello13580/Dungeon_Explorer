using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>캐릭터 선택 화면의 카드 하나를 담당한다.</summary>
public class CharacterCardUI : MonoBehaviour
{
    [Header("표시 요소")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TextMeshProUGUI classText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Header("선택 표시")]
    [SerializeField] private GameObject selectedOverlay;  // 선택됐을 때 표시할 오버레이
    [SerializeField] private Button selectButton;

    private CharacterData characterData;

    private void Start()
    {
        CharacterSelectManager.OnSelectionChanged += OnSelectionChanged;
    }

    private void OnDestroy()
    {
        CharacterSelectManager.OnSelectionChanged -= OnSelectionChanged;
    }

    public void Setup(CharacterData data)
    {
        characterData = data;

        if (classText != null)       classText.text = data.className;
        if (descriptionText != null) descriptionText.text = data.description;

        if (portraitImage != null && data.portrait != null)
            portraitImage.sprite = data.portrait;

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnCardClicked);
        }

        RefreshSelectedVisual();
    }

    private void OnCardClicked()
    {
        CharacterSelectManager.Instance.ToggleCharacter(characterData);
    }

    private void OnSelectionChanged(object sender, System.EventArgs e)
    {
        RefreshSelectedVisual();
    }

    private void RefreshSelectedVisual()
    {
        if (selectedOverlay == null || characterData == null) return;
        if (CharacterSelectManager.Instance == null) return;
        bool isSelected = CharacterSelectManager.Instance.IsSelected(characterData);
        selectedOverlay.SetActive(isSelected);
    }
}
