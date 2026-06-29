using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이벤트 노드 팝업 UI.
/// EventUI.Open()으로 열고, 선택지를 고르면 결과를 적용한 뒤 OnEventCompleted를 발생시킨다.
/// </summary>
public class EventUI : MonoBehaviour
{
    public static EventUI Instance { get; private set; }

    /// <summary>이벤트 선택 완료 시 발생. MapUI가 구독해서 맵 패널을 다시 연다.</summary>
    public static event EventHandler OnEventCompleted;

    [Header("패널")]
    [SerializeField] private GameObject panel;

    [Header("이미지")]
    [SerializeField] private Image eventImage;

    [Header("텍스트")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI resultText;

    [Header("선택지")]
    [SerializeField] private Transform choiceContainer;   // 선택지 버튼들의 부모
    [SerializeField] private GameObject choiceButtonPrefab; // Button + TextMeshProUGUI 프리팹

    [Header("확인 버튼 (결과 확인 후 닫기)")]
    [SerializeField] private Button confirmButton;

    private List<GameObject> spawnedButtons = new List<GameObject>();

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        panel.SetActive(false);
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
    }

    // ─── 공개 API ────────────────────────────────────────────────

    public void Open(EventNodeData eventData)
    {
        if (eventData == null) { Debug.LogWarning("[EventUI] EventNodeData가 null입니다."); return; }

        if (eventImage != null)
        {
            eventImage.sprite = eventData.eventImage;
            eventImage.gameObject.SetActive(eventData.eventImage != null);
        }

        titleText.text = eventData.eventTitle;
        descriptionText.text = eventData.eventDescription;
        if (resultText != null)
        {
            resultText.text = string.Empty;
            resultText.gameObject.SetActive(false);
        }

        if (confirmButton != null)
            confirmButton.gameObject.SetActive(false);

        BuildChoiceButtons(eventData.choices);
        panel.SetActive(true);
    }

    // ─── 내부 ────────────────────────────────────────────────────

    private void BuildChoiceButtons(EventChoice[] choices)
    {
        foreach (GameObject btn in spawnedButtons)
            Destroy(btn);
        spawnedButtons.Clear();

        if (choices == null) return;

        foreach (EventChoice choice in choices)
        {
            GameObject btnObj = Instantiate(choiceButtonPrefab, choiceContainer);
            spawnedButtons.Add(btnObj);

            TextMeshProUGUI label = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = choice.choiceText;

            Button btn = btnObj.GetComponentInChildren<Button>();
            EventChoice captured = choice;
            if (btn != null)
                btn.onClick.AddListener(() => OnChoiceSelected(captured));
        }
    }

    private void OnChoiceSelected(EventChoice choice)
    {
        ApplyOutcomes(choice.outcomes);

        // 선택지 버튼 숨기고 디스크립션을 결과 텍스트로 교체
        foreach (GameObject btn in spawnedButtons)
            btn.SetActive(false);

        string outcomeDesc = BuildOutcomeDescription(choice.outcomes);
        descriptionText.text = string.IsNullOrEmpty(outcomeDesc)
            ? choice.resultText
            : choice.resultText + "\n\n" + outcomeDesc;

        if (resultText != null)
            resultText.gameObject.SetActive(false);

        if (confirmButton != null)
            confirmButton.gameObject.SetActive(true);
    }

    private void OnConfirmClicked()
    {
        panel.SetActive(false);
        OnEventCompleted?.Invoke(this, EventArgs.Empty);
    }

    private string BuildOutcomeDescription(EventOutcome[] outcomes)
    {
        if (outcomes == null || outcomes.Length == 0) return string.Empty;

        var lines = new System.Text.StringBuilder();
        foreach (EventOutcome outcome in outcomes)
        {
            switch (outcome.outcomeType)
            {
                case EventOutcomeType.GoldGain:   lines.AppendLine($"골드 +{outcome.value}"); break;
                case EventOutcomeType.GoldLoss:   lines.AppendLine($"골드 -{outcome.value}"); break;
                case EventOutcomeType.HpHeal:     lines.AppendLine($"체력 +{outcome.value}"); break;
                case EventOutcomeType.HpDamage:   lines.AppendLine($"체력 -{outcome.value}"); break;
            }
        }
        return lines.ToString().TrimEnd();
    }

    private void ApplyOutcomes(EventOutcome[] outcomes)
    {
        if (outcomes == null) return;

        foreach (EventOutcome outcome in outcomes)
        {
            switch (outcome.outcomeType)
            {
                case EventOutcomeType.GoldGain:
                    GoldSystem.Instance?.AddGold(outcome.value);
                    break;

                case EventOutcomeType.GoldLoss:
                    GoldSystem.Instance?.SpendGold(outcome.value);
                    break;

                case EventOutcomeType.HpHeal:
                    foreach (Unit unit in UnitManager.Instance.GetFriendlyUnitList())
                        unit.GetComponent<HealthSystem>()?.Heal(outcome.value);
                    break;

                case EventOutcomeType.HpDamage:
                    foreach (Unit unit in UnitManager.Instance.GetFriendlyUnitList())
                        unit.GetComponent<HealthSystem>()?.Damage(outcome.value);
                    break;
            }
        }
    }
}
