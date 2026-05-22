using System.Collections.Generic;
using UnityEngine;

public class StaircaseMonoBehaviour : MonoBehaviour
{
	[Min(1)] public int width = 1;
	[Min(2)] public int stepCount = 2;

	[Header("층 번호 (0 = 1층, 1 = 2층 ...)")]
	public int endFloor = 1;

	public void Initialize()
	{
		float cellSize = LevelGrid.Instance.GetCellSize();
		int startFloor = LevelGrid.Instance.GetFloor(transform.position);

		Vector3 stepDir = GetStepDir(cellSize);
		Vector3 widthDir = new Vector3(-stepDir.z, 0f, stepDir.x);

		GridPosition[,] tiles = new GridPosition[stepCount, width];
		for (int step = 0; step < stepCount; step++)
		{
			int forcedFloor = step == 0 ? startFloor
							: step == stepCount - 1 ? endFloor
							: -1;

			for (int w = 0; w < width; w++)
			{
				Vector3 worldPos = transform.position + stepDir * step + widthDir * w;
				GridPosition auto = LevelGrid.Instance.GetGridPosition(worldPos);

				int tileFloor = forcedFloor >= 0 ? forcedFloor : auto.floor;
				tiles[step, w] = new GridPosition(auto.x, auto.z, tileFloor);
			}
		}

		for (int step = 0; step < stepCount; step++)
		{
			bool isExclusive = step > 0 && step < stepCount - 1;

			for (int w = 0; w < width; w++)
			{
				GridPosition pos = tiles[step, w];
				List<GridPosition> connections = new List<GridPosition>();

				if (step > 0)             connections.Add(tiles[step - 1, w]);
				if (step < stepCount - 1) connections.Add(tiles[step + 1, w]);
				if (w > 0)                connections.Add(tiles[step, w - 1]);
				if (w < width - 1)        connections.Add(tiles[step, w + 1]);

				PathFinding.Instance.RegisterStaircaseTile(pos, connections, isExclusive);

				if (isExclusive)
				{
					int otherFloor = (pos.floor == startFloor) ? endFloor : startFloor;
					GridPosition altPos = new GridPosition(pos.x, pos.z, otherFloor);
					PathFinding.Instance.RegisterStaircaseTile(altPos, connections, true);
				}
			}
		}
	}

	private Vector3 GetStepDir(float cellSize)
	{
		Vector3 fwd = -transform.forward; // pivot이 1층 방향을 향하므로 반전
		if (Mathf.Abs(fwd.x) >= Mathf.Abs(fwd.z))
			return fwd.x > 0 ? new Vector3(cellSize, 0f, 0f) : new Vector3(-cellSize, 0f, 0f);
		else
			return fwd.z > 0 ? new Vector3(0f, 0f, cellSize) : new Vector3(0f, 0f, -cellSize);
	}
}
