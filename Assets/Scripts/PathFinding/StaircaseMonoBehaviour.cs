using System.Collections.Generic;
using UnityEngine;

public class StaircaseMonoBehaviour : MonoBehaviour
{
	public enum StepDirection { PosX, NegX, PosZ, NegZ }

	[Header("계단 시작 타일 (startFloor 쪽, 너비 왼쪽 끝 타일)")]
	public Vector3 startTilePosition;

	[Header("계단이 올라가는 방향 (1층→2층 방향)")]
	public StepDirection stepDirection = StepDirection.NegZ;

	[Min(1)] public int width = 1;
	[Min(2)] public int stepCount = 2;

	[Header("층 번호 (0 = 1층, 1 = 2층 ...)")]
	public int startFloor = 0;
	public int endFloor   = 1;

	public void Initialize()
	{
		float cellSize = LevelGrid.Instance.GetCellSize();

		Vector3 stepDir = stepDirection switch
		{
			StepDirection.PosX => new Vector3( cellSize, 0f, 0f),
			StepDirection.NegX => new Vector3(-cellSize, 0f, 0f),
			StepDirection.PosZ => new Vector3(0f, 0f,  cellSize),
			StepDirection.NegZ => new Vector3(0f, 0f, -cellSize),
			_ => Vector3.zero
		};

		// 진행 방향의 오른쪽 수직 방향 (stepDir을 Y축 기준 반시계 90도 회전)
		Vector3 widthDir = new Vector3(-stepDir.z, 0f, stepDir.x);

		GridPosition[,] tiles = new GridPosition[stepCount, width];
		for (int step = 0; step < stepCount; step++)
		{
			// 시작·끝 줄은 층 강제, 중간 줄은 실제 pathfinding 그리드에서 감지한 층 사용
			int forcedFloor = step == 0 ? startFloor
							: step == stepCount - 1 ? endFloor
							: -1; // -1 = 자동 감지

			for (int w = 0; w < width; w++)
			{
				Vector3 worldPos = startTilePosition + stepDir * step + widthDir * w;
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
}
