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
			UnitActionSystem.Instance.SetSelectedAction(baseAction);
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
