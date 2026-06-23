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
			// SetSelectedUnit 호출 전에 현재 선택 상태를 먼저 읽어야 한다.
			// SetSelectedUnit 내부에서 SetSelectedAction(null)을 호출하므로
			// 순서가 바뀌면 current가 항상 null이 돼 토글이 동작하지 않는다.
			BaseAction current = UnitActionSystem.Instance.GetSelectedAction();
			UnitActionSystem.Instance.SetSelectedUnit(TurnSystem.Instance.GetTurnUnit());
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
