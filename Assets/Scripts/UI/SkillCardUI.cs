using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스킬 선택 UI에서 카드 하나를 담당한다.
/// 카드 클릭 시 직접 스킬을 습득하지 않고 SkillUnlockUI에 선택 알림만 보낸다.
/// </summary>
public class SkillCardUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI skillNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI unitClassText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button selectButton;
    [SerializeField] private GameObject selectedOverlay; // 선택됐을 때 표시할 비주얼

    [Header("코스트")]
    [SerializeField] private Transform actionPointContainer;
    [SerializeField] private GameObject actionPointIconPrefab;
    [SerializeField] private Transform jobPointContainer;
    [SerializeField] private GameObject jobPointIconPrefab;
    [SerializeField] private TextMeshProUGUI manaCostText;

    private readonly List<GameObject> spawnedCostIcons = new List<GameObject>();

    private SkillUnlockManager.SkillUnlockOption option;
    private Action<SkillUnlockManager.SkillUnlockOption> onSelected;

    /// <summary>
    /// SkillUnlockUI에서 호출. onSelected 콜백으로 선택 알림을 SkillUnlockUI에 전달한다.
    /// </summary>
    public void Setup(SkillUnlockManager.SkillUnlockOption option,
                      Action<SkillUnlockManager.SkillUnlockOption> onSelected)
    {
        this.option = option;
        this.onSelected = onSelected;

        if (skillNameText != null) skillNameText.text = option.skillDef.skillName;
        if (unitClassText != null)  unitClassText.text = option.unitClassId;

        if (descriptionText != null)
        {
            // 유닛에 이미 붙어있는 액션 컴포넌트에서 실제 스탯 반영 설명을 가져온다.
            // GetDescription()이 빈 문자열을 반환하면 ScriptableObject의 정적 설명을 사용한다.
            string dynamicDesc = "";
            if (option.targetUnit != null)
            {
                System.Type actionType = FindActionType(option.skillDef.actionTypeName);
                if (actionType != null)
                {
                    BaseAction action = option.targetUnit.GetComponent(actionType) as BaseAction;
                    if (action != null)
                        dynamicDesc = action.GetDescription();
                }
            }
            descriptionText.text = string.IsNullOrEmpty(dynamicDesc)
                ? option.skillDef.description
                : dynamicDesc;
        }

        if (iconImage != null && option.skillDef.icon != null)
            iconImage.sprite = option.skillDef.icon;

        // 코스트 아이콘 — 액션 컴포넌트가 있을 때만 표시
        RefreshCostIcons(option);

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnCardClicked);
        }

        SetSelected(false);
    }

    private void RefreshCostIcons(SkillUnlockManager.SkillUnlockOption option)
    {
        foreach (GameObject icon in spawnedCostIcons)
            Destroy(icon);
        spawnedCostIcons.Clear();

        if (option.targetUnit == null) return;

        System.Type actionType = FindActionType(option.skillDef.actionTypeName);
        if (actionType == null) return;

        BaseAction action = option.targetUnit.GetComponent(actionType) as BaseAction;
        if (action == null) return;

        if (actionPointContainer != null && actionPointIconPrefab != null)
            for (int i = 0; i < action.GetActionPointCost(); i++)
                spawnedCostIcons.Add(Instantiate(actionPointIconPrefab, actionPointContainer));

        if (jobPointContainer != null && jobPointIconPrefab != null)
            for (int i = 0; i < action.GetJobPointCost(); i++)
                spawnedCostIcons.Add(Instantiate(jobPointIconPrefab, jobPointContainer));

        if (manaCostText != null)
            manaCostText.text = $"마나: {action.GetManaCost()}";
    }

    private void OnCardClicked()
    {
        // 직접 스킬 습득하지 않고 SkillUnlockUI에 선택 알림만 전달
        onSelected?.Invoke(option);
    }

    /// <summary>SkillUnlockUI에서 선택 상태를 외부에서 설정할 때 호출.</summary>
    public void SetSelected(bool isSelected)
    {
        if (selectedOverlay != null)
            selectedOverlay.SetActive(isSelected);
    }

    public SkillUnlockManager.SkillUnlockOption GetOption() => option;

    private static System.Type FindActionType(string typeName)
    {
        foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type type = assembly.GetType(typeName);
            if (type != null) return type;
        }
        return null;
    }
}
