using UnityEngine;

public class PathfindingLinkMonoBehaviour : MonoBehaviour
{
	public Vector3 linkPositionA;

	public Vector3 linkPositionB;

	public PathFindingLink GetPathfindingLink()
	{
		return new PathFindingLink
		{
			gridPositionA = LevelGrid.Instance.GetGridPosition(linkPositionA),
			gridPositionB = LevelGrid.Instance.GetGridPosition(linkPositionB)
		};
	}
}
