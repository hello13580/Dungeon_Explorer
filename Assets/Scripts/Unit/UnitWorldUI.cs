using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitWorldUI : MonoBehaviour
{
	[SerializeField]
	private TextMeshProUGUI actionPointsText;

	[SerializeField]
	private Image healthBarImage;

	[SerializeField]
	private HealthSystem healthSystem;

	[SerializeField]
	private Unit unit;

	private void Start()
	{
		Unit.OnAnyActionPointsChanged += Unit_OnAnyActionPointsChanged;
		healthSystem.OnUnitDamaged += HealthSystem_OnUnitDamaged;
		UpdateActionPointText();
		UpdateHealthBar();
	}

	private void UpdateActionPointText()
	{
		actionPointsText.text = unit.GetCurrentActionPoint().ToString();
	}

	private void Unit_OnAnyActionPointsChanged(object sender, EventArgs empty)
	{
		UpdateActionPointText();
	}

	private void UpdateHealthBar()
	{
		healthBarImage.fillAmount = healthSystem.GetHealthNormalized();
	}

	private void HealthSystem_OnUnitDamaged(object sender, EventArgs empty)
	{
		UpdateHealthBar();
	}
}
