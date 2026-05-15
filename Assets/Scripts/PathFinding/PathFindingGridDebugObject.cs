using TMPro;
using UnityEngine;

public class PathFindingGridDebugObject : GridDebugObject
{
	[SerializeField]
	private TextMeshPro gCostText;

	[SerializeField]
	private TextMeshPro hCostText;

	[SerializeField]
	private TextMeshPro fCostText;

	private PathNode pathNode;

	public override void SetGridObject(object gridObject)
	{
		base.SetGridObject(gridObject);
		pathNode = (PathNode)gridObject;
	}

	protected override void SetGridText()
	{
		base.SetGridText();
		gCostText.text = pathNode.GetGCost().ToString();
		hCostText.text = pathNode.GetHCost().ToString();
		fCostText.text = pathNode.GetFCost().ToString();
	}
}
