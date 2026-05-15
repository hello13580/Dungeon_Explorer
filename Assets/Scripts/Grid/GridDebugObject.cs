using TMPro;
using UnityEngine;

public class GridDebugObject : MonoBehaviour
{
	private object gridObject;

	[SerializeField]
	private TextMeshPro gridText;

	protected void Start()
	{
		SetGridText();
	}

	protected void Update()
	{
		SetGridText();
	}

	public virtual void SetGridObject(object gridObject)
	{
		this.gridObject = gridObject;
	}

	protected virtual void SetGridText()
	{
		gridText.text = gridObject.ToString();
	}
}
