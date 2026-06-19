using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 액션 버튼에 마우스를 올리고 지정 키를 누르고 있으면 스킬 설명을 표시하는 툴팁 패널.
/// </summary>
public class SkillTooltipUI : MonoBehaviour
{
    public static SkillTooltipUI Instance { get; private set; }

    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI skillNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Key tooltipKey = Key.T;

    [Header("코스트 아이콘")]
    [SerializeField] private Transform actionPointContainer;  // AP 아이콘을 생성할 부모
    [SerializeField] private GameObject actionPointIconPrefab;

    [SerializeField] private Transform jobPointContainer;     // JP 아이콘을 생성할 부모
    [SerializeField] private GameObject jobPointIconPrefab;

    [SerializeField] private TextMeshProUGUI manaCostText;    // 마나 소모량 텍스트

    private BaseAction hoveredAction;
    private readonly List<GameObject> spawnedIcons = new List<GameObject>();

    private void Awake()
    {
        Instance = this;
        panel.SetActive(false);
    }

    private void Update()
    {
        if (hoveredAction == null)
        {
            panel.SetActive(false);
            return;
        }

        bool keyHeld = Keyboard.current != null && Keyboard.current[tooltipKey].isPressed;

        // 지정 키를 누르고 있는 동안만 표시
        panel.SetActive(keyHeld);
    }

    /// <summary>ActionButtonUI에서 마우스가 버튼 위에 올라왔을 때 호출.</summary>
    public void Show(BaseAction action)
    {
        hoveredAction = action;

        if (skillNameText != null)
            skillNameText.text = action.GetActionName();

        if (descriptionText != null)
        {
            string desc = action.GetDescription();
            descriptionText.text = string.IsNullOrEmpty(desc) ? "설명 없음" : desc;
        }

        RefreshCostIcons(action);
    }

    /// <summary>ActionButtonUI에서 마우스가 버튼을 벗어났을 때 호출.</summary>
    public void Hide()
    {
        hoveredAction = null;
        panel.SetActive(false);
    }

    public Key GetTooltipKey() => tooltipKey;

    // ── 코스트 아이콘 ────────────────────────────────────────────────

    /// <summary>기존 아이콘을 모두 제거하고 액션 코스트만큼 새로 생성한다.</summary>
    private void RefreshCostIcons(BaseAction action)
    {
        // 기존 아이콘 제거
        foreach (GameObject icon in spawnedIcons)
            Destroy(icon);
        spawnedIcons.Clear();

        // 액션 포인트 아이콘
        if (actionPointContainer != null && actionPointIconPrefab != null)
            SpawnIcons(actionPointContainer, actionPointIconPrefab, action.GetActionPointCost());

        // 잡 포인트 아이콘 (소모가 없으면 0개 = 표시 안 함)
        if (jobPointContainer != null && jobPointIconPrefab != null)
            SpawnIcons(jobPointContainer, jobPointIconPrefab, action.GetJobPointCost());

        // 마나 소모량 텍스트 (0이면 숨김)
        if (manaCostText != null)
        {
            int mana = action.GetManaCost();
            manaCostText.text = mana > 0 ? $"마나: {mana}" : "";
        }
    }

    private void SpawnIcons(Transform container, GameObject prefab, int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject icon = Instantiate(prefab, container);
            spawnedIcons.Add(icon);
        }
    }
}
