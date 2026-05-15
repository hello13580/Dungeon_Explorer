using System;
using UnityEngine;

public class InteractSphere : MapObject
{
	[SerializeField]
	private Material blueMaterial;

	[SerializeField]
	private Material redMaterial;

	[SerializeField]
	private MeshRenderer meshRenderer;

	private bool isBlue;

	protected override void Start()
	{
		base.Start();
		SetColorRed();
	}

	private void SetColorBlue()
	{
		isBlue = true;
		meshRenderer.material = blueMaterial;
	}

	private void SetColorRed()
	{
		isBlue = false;
		meshRenderer.material = redMaterial;
	}

	public override string GetObjectName()
	{
		return "상호작용 구체";
	}

	public override void Interact(Action onInteractComplete)
	{
		if (isBlue)
		{
			SetColorRed();
		}
		else
		{
			SetColorBlue();
		}
		onInteractComplete();
	}
}
