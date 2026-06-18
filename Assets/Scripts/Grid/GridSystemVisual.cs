using System;
using System.Collections.Generic;
using UnityEngine;

public class GridSystemVisual : MonoBehaviour
{
    [Serializable]
    public struct GridVisualTypeMaterial
    {
        public GridVisualType gridVisualType;

        public Material material;
    }

    public enum GridVisualType
    {
        White,
        Blue,
        Red,
        Yellow,
        Green
    }

    [SerializeField]
    private GameObject gridSystemVisualSinglePrefab;

    [SerializeField]
    private List<GridVisualTypeMaterial> gridVisualTypeMaterialList;

    // 계단·경사로처럼 실제 지면이 그리드 높이(y=floor*FLOOR_HEIGHT)와 다를 때
    // 그리드 비주얼을 실제 지면 표면에 붙이기 위한 레이어마스크
    [SerializeField] private LayerMask groundSnapLayerMask;

    private GridSystemVisualSingle[,,] gridSystemVisualSingleArray;

    // 현재 화면에 표시 중인 타일만 추적
    // Hide 시 전체 7500개 타일을 순회하는 대신 이 목록만 순회해서 성능 개선
    private HashSet<GridSystemVisualSingle> visibleTiles = new HashSet<GridSystemVisualSingle>();

    private GridPosition lastMouseGridPosition = new GridPosition(-1, -1, 0);

    private BaseAction selectedAction;

    private bool isVisualHidden = true;

    public static GridSystemVisual Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void Start()
    {
        // 이벤트 구독만 수행 — 그리드 비주얼 생성은 StageManager가 맵 로드 후 Initialize()를 호출
        UnitActionSystem.Instance.OnSelectedUnitChanged += delegate
        {
            HideAllGridPositionFade();
        };
        UnitActionSystem.Instance.OnSelectedActionChanged += delegate (object s, BaseAction action)
        {
            selectedAction = action;
            UpdateGridVisual();
        };
        BaseAction.OnAnyActionStarted += BaseAction_OnAnyActionStarted;
        BaseAction.OnAnyActionEnded += delegate
        {
            UpdateGridVisual();
        };
        TurnSystem.Instance.OnTurnChanged += delegate
        {
            selectedAction = null;
            HideAllGridPositionFade();
        };
    }

    /// <summary>
    /// 맵 프리팹 인스턴시에이트 후 StageManager에서 호출.
    /// 기존 비주얼을 제거하고 현재 씬 지형 기준으로 새로 생성한다.
    /// </summary>
    public void Initialize()
    {
        // 기존 비주얼 오브젝트 제거
        if (gridSystemVisualSingleArray != null)
        {
            foreach (GridSystemVisualSingle visual in gridSystemVisualSingleArray)
            {
                if (visual != null) Destroy(visual.gameObject);
            }
        }
        visibleTiles.Clear();
        selectedAction = null;

        // 새 배열 생성 및 비주얼 인스턴시에이트
        gridSystemVisualSingleArray = new GridSystemVisualSingle[LevelGrid.Instance.GetWidth(), LevelGrid.Instance.GetHeight(), LevelGrid.Instance.GetFloorAmount()];
        for (int i = 0; i < LevelGrid.Instance.GetWidth(); i++)
        {
            for (int j = 0; j < LevelGrid.Instance.GetHeight(); j++)
            {
                for (int k = 0; k < LevelGrid.Instance.GetFloorAmount(); k++)
                {
                    GridPosition gridPosition = new GridPosition(i, j, k);

                    Vector3 spawnPos = LevelGrid.Instance.GetWorldPosition(gridPosition);
                    Quaternion spawnRot = Quaternion.identity;

                    if (groundSnapLayerMask != 0 &&
                        Physics.Raycast(spawnPos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 3f, groundSnapLayerMask))
                    {
                        spawnPos.y = hit.point.y + 0.05f;
                        spawnRot = Quaternion.FromToRotation(Vector3.up, hit.normal);
                    }

                    GameObject val = Instantiate(gridSystemVisualSinglePrefab, spawnPos, spawnRot);
                    gridSystemVisualSingleArray[i, j, k] = val.GetComponent<GridSystemVisualSingle>();
                    visibleTiles.Add(gridSystemVisualSingleArray[i, j, k]);
                }
            }
        }

        HideAllGridPositionInstant();
        isVisualHidden = true;
    }

    private void Update()
    {
        if (selectedAction == null)
        {
            return;
        }
        if (UnitActionSystem.Instance.IsBusy())
        {
            if (!isVisualHidden)
            {
                isVisualHidden = true;
                lastMouseGridPosition = new GridPosition(-1, -1, 0);
            }
            return;
        }
        if (isVisualHidden)
        {
            isVisualHidden = false;
            UpdateGridVisual();
            return;
        }
        GridPosition gridPosition = LevelGrid.Instance.GetGridPosition(MouseWorld.GetPosition());
        if (!LevelGrid.Instance.IsValidGridPosition(gridPosition))
        {
            if (lastMouseGridPosition != new GridPosition(-1, -1, 0))
            {
                lastMouseGridPosition = new GridPosition(-1, -1, 0);
                UpdateGridVisual();
            }
        }
        else if (!(gridPosition == lastMouseGridPosition))
        {
            lastMouseGridPosition = gridPosition;
            UpdateGridVisual();
        }
    }

    public void UpdateGridVisual()
    {
        if (gridSystemVisualSingleArray == null) return;
        HideAllGridPositionInstant();
        if (selectedAction == null)
        {
            return;
        }
        ShowGridPositionListInstant(selectedAction.GetActionRangeGridPositionList(), GridVisualType.White);
        ShowGridPositionListInstant(selectedAction.GetValidActionGridPositionList(), GetGridVisualTypeForAction(selectedAction));
        ShowGridPositionListInstant(selectedAction.GetSecondaryHighlightGridPositionList(), selectedAction.GetSecondaryHighlightColor());
        GridPosition gridPosition = LevelGrid.Instance.GetGridPosition(MouseWorld.GetPosition());
        if (selectedAction.IsValidActionGridPosition(gridPosition))
        {
            List<GridPosition> list = new List<GridPosition>();
            list = ((!(selectedAction is MoveAction moveAction)) ? selectedAction.GetDamageAffectedGridPosition(gridPosition) : moveAction.GetMovementPreviewGridPositionList(gridPosition));
            ShowGridPositionListInstant(list, GridVisualType.Red);
        }
    }

    private void BaseAction_OnAnyActionStarted(object sender, EventArgs e)
    {
        if (sender is MoveAction)
        {
            HideAllGridPositionFade();
        }
        else
        {
            HideAllGridPositionInstant();
        }
        isVisualHidden = true;
    }

    public void ShowGridPositionListInstant(List<GridPosition> gridPositionList, GridVisualType gridVisualType)
    {
        Material gridVisualTypeMaterial = GetGridVisualTypeMaterial(gridVisualType);
        foreach (GridPosition gridPosition in gridPositionList)
        {
            if (LevelGrid.Instance.IsValidGridPosition(gridPosition))
            {
                GridSystemVisualSingle tile = gridSystemVisualSingleArray[gridPosition.x, gridPosition.z, gridPosition.floor];
                tile.InstantShow(gridVisualTypeMaterial);
                // 표시한 타일을 목록에 추가해서 나중에 Hide 시 이 타일만 처리할 수 있게 함
                visibleTiles.Add(tile);
            }
        }
    }

    public void HideAllGridPositionInstant()
    {
        // [최적화 전] 전체 타일(50×50×3 = 7500개)을 매번 순회 → 마우스 이동마다 7500번 호출
        // GridSystemVisualSingle[,,] array = gridSystemVisualSingleArray;
        // int upperBound = array.GetUpperBound(0);
        // int upperBound2 = array.GetUpperBound(1);
        // int upperBound3 = array.GetUpperBound(2);
        // for (int i = array.GetLowerBound(0); i <= upperBound; i++)
        // {
        // 	for (int j = array.GetLowerBound(1); j <= upperBound2; j++)
        // 	{
        // 		for (int k = array.GetLowerBound(2); k <= upperBound3; k++)
        // 		{
        // 			array[i, j, k].InstantHide();
        // 		}
        // 	}
        // }

        // [최적화 후] 실제로 표시 중인 타일만 순회 → 보통 수십~백여 개 수준
        foreach (GridSystemVisualSingle tile in visibleTiles)
        {
            tile.InstantHide();
        }
        visibleTiles.Clear();
    }

    public void HideAllGridPositionFade()
    {
        // [최적화 전] HideAllGridPositionInstant와 동일하게 전체 타일 순회
        // GridSystemVisualSingle[,,] array = gridSystemVisualSingleArray;
        // int upperBound = array.GetUpperBound(0);
        // int upperBound2 = array.GetUpperBound(1);
        // int upperBound3 = array.GetUpperBound(2);
        // for (int i = array.GetLowerBound(0); i <= upperBound; i++)
        // {
        // 	for (int j = array.GetLowerBound(1); j <= upperBound2; j++)
        // 	{
        // 		for (int k = array.GetLowerBound(2); k <= upperBound3; k++)
        // 		{
        // 			array[i, j, k].FadeHide();
        // 		}
        // 	}
        // }

        // [최적화 후] visibleTiles만 순회
        foreach (GridSystemVisualSingle tile in visibleTiles)
        {
            tile.FadeHide();
        }
        visibleTiles.Clear();
    }

    private GridVisualType GetGridVisualTypeForAction(BaseAction action)
    {
        return action switch
        {
            MoveAction => GridVisualType.Green,
            SpinAction => GridVisualType.Green,
            ShootAction => GridVisualType.Green,
            BowAction => GridVisualType.Green,
            AOEAction => GridVisualType.Green,
            MeleeAction => GridVisualType.Green,
            InteractAction => GridVisualType.Green,
            HealAction => GridVisualType.Green,
            BarrierAction => GridVisualType.Green,
            TeleportAction => GridVisualType.Green,
            WindBlastAction => GridVisualType.Green,
            TauntAction => GridVisualType.Yellow,
            PersistentAOEAction => GridVisualType.Green,
            IceOrbAction => GridVisualType.Green,
            DashAttackAction => GridVisualType.Green,
            BarrierAuraAction => GridVisualType.Blue,
            AttackAuraAction => GridVisualType.Red,
            _ => GridVisualType.White,
        };
    }

    private Material GetGridVisualTypeMaterial(GridVisualType gridVisualType)
    {
        foreach (GridVisualTypeMaterial gridVisualTypeMaterial in gridVisualTypeMaterialList)
        {
            if (gridVisualTypeMaterial.gridVisualType == gridVisualType)
            {
                return gridVisualTypeMaterial.material;
            }
        }
        return null;
    }
}
