using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ActionButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
	[SerializeField]
	private TextMeshProUGUI textMeshPro;

	[SerializeField]
	private GameObject selectedImage;

	[SerializeField]
	private Button button;

	private BaseAction baseAction;

	public void SetBaseAction(BaseAction baseAction)
	{
		this.baseAction = baseAction;
		textMeshPro.text = baseAction.GetActionName().ToUpper();
		button.onClick.AddListener(delegate
		{
			UnitActionSystem.Instance.SetSelectedUnit(TurnSystem.Instance.GetTurnUnit());
			// 이미 선택된 액션을 다시 누르면 선택 취소
			BaseAction current = UnitActionSystem.Instance.GetSelectedAction();
			UnitActionSystem.Instance.SetSelectedAction(current == baseAction ? null : baseAction);
		});
	}

	public void UpdateSelectedVisual()
	{
		BaseAction selectedAction = UnitActionSystem.Instance.GetSelectedAction();
		selectedImage.SetActive(selectedAction == baseAction);
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		if (SkillTooltipUI.Instance != null)
			SkillTooltipUI.Instance.Show(baseAction);
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		if (SkillTooltipUI.Instance != null)
			SkillTooltipUI.Instance.Hide();
	}
}
