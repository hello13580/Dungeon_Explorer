using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RestUI : MonoBehaviour
{
    public static RestUI Instance { get; private set; }
    public static event EventHandler OnRestCompleted;

    [Header("패널")]
    [SerializeField] private GameObject panel;

    [Header("캐릭터 정보")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI progressText;

    [Header("버튼")]
    [SerializeField] private Button healButton;
    [SerializeField] private TextMeshProUGUI healButtonText;
    [SerializeField] private Button reviveButton;
    [SerializeField] private TextMeshProUGUI reviveButtonText;
    [SerializeField] private Button nextButton;
    [SerializeField] private TextMeshProUGUI nextButtonText;

    private List<Unit> queue = new List<Unit>();
    private int currentIndex;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        panel.SetActive(false);
        healButton.onClick.AddListener(OnHealClicked);
        reviveButton.onClick.AddListener(OnReviveClicked);
        nextButton.onClick.AddListener(OnNextClicked);
    }

    public void Open()
    {
        queue.Clear();
        queue.AddRange(PartyManager.Instance.GetPartyUnits());
        queue.AddRange(PartyManager.Instance.GetIncapacitatedUnits());

        if (queue.Count == 0)
        {
            OnRestCompleted?.Invoke(this, EventArgs.Empty);
            return;
        }

        currentIndex = 0;
        panel.SetActive(true);
        ShowCurrent();
    }

    private void ShowCurrent()
    {
        Unit unit = queue[currentIndex];
        bool isIncapacitated = PartyManager.Instance.GetIncapacitatedUnits().Contains(unit);

        HealthSystem hs = unit.GetComponent<HealthSystem>();
        int maxHp = hs != null ? hs.GetMaxHealth() : 0;

        nameText.text = unit.GetUnitName();
        statusText.text = isIncapacitated ? "무력화" : $"HP {unit.GetCurrentHealth()} / {maxHp}";
        if (progressText != null)
            progressText.text = $"{currentIndex + 1} / {queue.Count}";

        healButton.gameObject.SetActive(!isIncapacitated);
        healButton.interactable = true;
        if (!isIncapacitated)
        {
            int healAmt = Mathf.RoundToInt(maxHp * 0.4f);
            healButtonText.text = $"회복\n최대체력의 40% ({healAmt})를 회복한다";
        }

        reviveButton.gameObject.SetActive(isIncapacitated);
        reviveButton.interactable = true;
        if (isIncapacitated)
        {
            int reviveHp = Mathf.RoundToInt(maxHp * 0.2f);
            reviveButtonText.text = $"부활\n최대체력의 20% ({reviveHp})로 부활한다";
        }

        bool isLast = currentIndex == queue.Count - 1;
        if (nextButtonText != null)
            nextButtonText.text = isLast ? "완료" : "다음";
    }

    private void OnHealClicked()
    {
        Unit unit = queue[currentIndex];
        HealthSystem hs = unit.GetComponent<HealthSystem>();
        if (hs != null)
            hs.Heal(Mathf.RoundToInt(hs.GetMaxHealth() * 0.4f));

        healButton.interactable = false;
    }

    private void OnReviveClicked()
    {
        Unit unit = queue[currentIndex];
        HealthSystem hs = unit.GetComponent<HealthSystem>();
        int reviveHp = hs != null ? Mathf.RoundToInt(hs.GetMaxHealth() * 0.2f) : 1;
        PartyManager.Instance.ReviveUnit(unit, reviveHp);

        reviveButton.interactable = false;
    }

    private void OnNextClicked()
    {
        currentIndex++;
        if (currentIndex < queue.Count)
        {
            ShowCurrent();
        }
        else
        {
            panel.SetActive(false);
            OnRestCompleted?.Invoke(this, EventArgs.Empty);
        }
    }
}
