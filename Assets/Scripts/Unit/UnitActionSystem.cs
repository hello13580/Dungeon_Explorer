using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class UnitActionSystem : MonoBehaviour
{
    public static UnitActionSystem Instance { get; private set; }

    [SerializeField] private Unit selectedUnit;
    [SerializeField] private LayerMask unitLayerMask;

    private BaseAction selectedAction;
    private bool isBusy;

    // �̺�Ʈ: UI�� �ٸ� �ý��ۿ��� ������ �� �ֵ��� ��
    public event EventHandler<Unit> OnSelectedUnitChanged;
    public event EventHandler<BaseAction> OnSelectedActionChanged;
    public event EventHandler<bool> OnBusyChanged;

    private void Awake()
    {
        // �̱��� �ʱ�ȭ
        if (Instance != null && Instance != this)
        {
            Debug.LogError($"There's more than one UnitActionSystem! {transform} - {Instance}");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad(gameObject); // �ʿ� �� ����
    }

    private void Start()
    {
        // �ܺ� �ý��� �̺�Ʈ ����
        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
        BaseAction.OnAnyActionEnded += BaseAction_OnAnyActionEnded;
    }

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        // ���� �ٲ�� ���õ� �׼� �ʱ�ȭ
        SetSelectedAction(null);
    }

    private void Update()
    {
        // 1. UI ���� Ŭ�� ���̰ų� �ý����� �ٻ� ����(�ִϸ��̼� �� ��)��� �Է� ����
        if (EventSystem.current.IsPointerOverGameObject()) return;
        if (isBusy) return;

        HandleSelectedAction();
    }

    private void HandleSelectedAction()
    {
        // ���� Ŭ��: ���� ����
        if (InputManager.Instance.IsMouseButtonDownThisFrame(0))
        {
            TryHandleUnitSelection();
        }

        // ������ Ŭ��: �׼� ���� (�÷��̾� ���� ����)
        if (TurnSystem.Instance.IsPlayerTurn() && InputManager.Instance.IsMouseButtonDownThisFrame(1))
        {
            HandleActionExecution();
        }
    }

    private void TryHandleUnitSelection()
    {
        // ���콺 ��ġ���� ����ĳ��Ʈ�� ��� ���� Ȯ��
        Ray ray = Camera.main.ScreenPointToRay(InputManager.Instance.GetMouseScreenPosition());
        if (Physics.Raycast(ray, out RaycastHit raycastHit, float.MaxValue, unitLayerMask))
        {
            if (raycastHit.collider.TryGetComponent<Unit>(out Unit unit))
            {
                // �̹� ���õ� ������ �� �����ϴ� �� ����
                if (unit == selectedUnit) return;

                SetSelectedUnit(unit);
            }
        }
    }

    private void HandleActionExecution()
    {
        Vector3 mouseWorldPosition = MouseWorld.GetPosition();

        // ��ȿ���� ���� ���콺 ��ġ üũ
        if (mouseWorldPosition == Vector3.negativeInfinity) return;
        if (selectedUnit == null || selectedAction == null) return;

        GridPosition targetGridPosition = LevelGrid.Instance.GetGridPosition(mouseWorldPosition);
        //Debug.Log($"[Click] world={mouseWorldPosition} → grid=x:{targetGridPosition.x}, z:{targetGridPosition.z}, floor:{targetGridPosition.floor}, valid={selectedAction?.IsValidActionGridPosition(targetGridPosition)}");

        // ���� ����: �� �����ΰ�? + �� ���ΰ�? + ��Ÿ� ���ΰ�? + ����Ʈ�� ����Ѱ�?
        if (selectedUnit.GetTeamType() == TeamType.Player &&
            IsSelectedUnitTurn() &&
            selectedAction.IsValidActionGridPosition(targetGridPosition) &&
            selectedUnit.CanTakeAction(selectedAction))
        {
            if (selectedUnit.SpendActionPoint(selectedAction))
            {
                SetBusy();
                selectedAction.TakeAction(targetGridPosition, ClearBusy);
            }
        }
    }

    private void SetBusy()
    {
        isBusy = true;
        OnBusyChanged?.Invoke(this, isBusy);
    }

    private void ClearBusy()
    {
        isBusy = false;
        OnBusyChanged?.Invoke(this, isBusy);
    }

    public void SetSelectedUnit(Unit unit)
    {
        selectedUnit = unit;
        SetSelectedAction(null); // ������ �ٲ�� �׼� ����â �ʱ�ȭ
        OnSelectedUnitChanged?.Invoke(this, selectedUnit);
    }

    public void SetSelectedAction(BaseAction baseAction)
    {
        selectedAction = baseAction;
        OnSelectedActionChanged?.Invoke(this, baseAction);
    }

    public Unit GetSelectedUnit() => selectedUnit;
    public BaseAction GetSelectedAction() => selectedAction;
    public bool IsBusy() => isBusy;

    public bool IsSelectedUnitTurn()
    {
        if (selectedUnit == null) return false;
        return selectedUnit == TurnSystem.Instance.GetTurnUnit();
    }

    private void BaseAction_OnAnyActionEnded(object sender, EventArgs empty)
    {
        // � ������ �׼��̵� ����Ǹ� ���� ���� ���� (�ʿ信 ���� ���� ����)
        SetSelectedAction(null);
    }
}