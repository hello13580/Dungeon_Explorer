using UnityEngine;

public class UnitSelectedVisual : MonoBehaviour
{
	[SerializeField]
	private Unit unit;

	private MeshRenderer meshRenderer;

	private void Awake()
	{
		meshRenderer = GetComponent<MeshRenderer>();
	}

	private void Start()
	{
		UnitActionSystem.Instance.OnSelectedUnitChanged += UnitActionSystem_OnselectedUnitChanged;
		UpdateVisual(UnitActionSystem.Instance.GetSelectedUnit());
	}

	private void UnitActionSystem_OnselectedUnitChanged(object sender, Unit selectedUnit)
	{
		UpdateVisual(selectedUnit);
	}

	private void UpdateVisual(Unit selectedUnit)
	{
		if (selectedUnit == unit)
		{
			meshRenderer.enabled = true;
		}
		else
		{
			meshRenderer.enabled = false;
		}
	}

	private void OnDestroy()
	{
		UnitActionSystem.Instance.OnSelectedUnitChanged -= UnitActionSystem_OnselectedUnitChanged;
	}
}
