using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ActionButtonUI : MonoBehaviour
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
			UnitActionSystem.Instance.SetSelectedAction(baseAction);
		});
	}

	public void UpdateSelectedVisual()
	{
		BaseAction selectedAction = UnitActionSystem.Instance.GetSelectedAction();
		selectedImage.SetActive(selectedAction == baseAction);
	}
}
