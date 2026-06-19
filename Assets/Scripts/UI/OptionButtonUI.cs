using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ActionSelectionUI에서 동적으로 생성되는 선택지 버튼 하나.
/// </summary>
public class OptionButtonUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Button button;

    public void Setup(string optionName, string description, Action onClick)
    {
        if (nameText != null)        nameText.text = optionName;
        if (descriptionText != null) descriptionText.text = description;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke());
    }
}
