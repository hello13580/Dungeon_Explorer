using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>스킬 선택 UI에서 카드 하나를 담당한다.</summary>
public class SkillCardUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI skillNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI unitClassText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button selectButton;

    private SkillUnlockManager.SkillUnlockOption option;

    public void Setup(SkillUnlockManager.SkillUnlockOption option)
    {
        this.option = option;

        if (skillNameText != null)  skillNameText.text  = option.skillDef.skillName;
        if (descriptionText != null) descriptionText.text = option.skillDef.description;
        if (unitClassText != null)  unitClassText.text  = option.unitClassId;

        if (iconImage != null && option.skillDef.icon != null)
        {
            iconImage.sprite = option.skillDef.icon;
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelectClicked);
        }
    }

    private void OnSelectClicked()
    {
        SkillUnlockManager.Instance.ConfirmUnlock(option);
    }
}
