using UnityEngine;

public class UnitVisibility : MonoBehaviour
{
	[SerializeField]
	private bool dynamicFloorPosition;

	[SerializeField]
	private Material opaqueMaterial;

	[SerializeField]
	private Material transparentMaterial;

	[SerializeField]
	private Renderer bodyRenderer;

	private Renderer[] rendererArray;

	private int floor;

	private bool isShowing = true;

	[SerializeField]
	private float transparentAlpha = 0.3f;

	private void Awake()
	{
		rendererArray = GetComponentsInChildren<Renderer>(true);
	}

	private void Start()
	{
		floor = LevelGrid.Instance.GetFloor(transform.position);
		UpdateVisibility(CameraController.Instance.GetCameraHeight());
		if (floor == 0 && !dynamicFloorPosition)
		{
			Destroy(this);
		}
	}

	private void Update()
	{
		if (dynamicFloorPosition)
		{
			floor = LevelGrid.Instance.GetFloor(transform.position);
		}
		UpdateVisibility(CameraController.Instance.GetCameraHeight());
	}

	private void UpdateVisibility(float cameraHeight)
	{
		float num = 3f * (float)floor;
		float num2 = 0.7f;
		bool flag = cameraHeight >= num + num2;
		if (flag != isShowing)
		{
			isShowing = flag;
			if (isShowing || floor == 0)
			{
				Show();
			}
			else
			{
				Hide();
			}
		}
	}

	private void Show()
	{
		bodyRenderer.material = opaqueMaterial;
	}

	private void Hide()
	{
		bodyRenderer.material = transparentMaterial;
	}
}
