using System;
using UnityEngine;

/// <summary>
/// 도발 상태를 전역으로 관리하는 싱글톤.
/// 도발 중인 유닛과 남은 턴 수를 추적하며 TurnSystem 이벤트로 카운트다운한다.
/// </summary>
public class TauntManager : MonoBehaviour
{
    public static TauntManager Instance { get; private set; }

    private Unit tauntedUnit;
    private int turnsRemaining;

    public event EventHandler OnTauntChanged;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
    }

    private void OnDestroy()
    {
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
    }

    /// <summary>도발 시작. 같은 유닛이 다시 도발하면 지속 시간을 갱신한다.</summary>
    public void ApplyTaunt(Unit unit, int turns)
    {
        tauntedUnit = unit;
        turnsRemaining = turns;
        OnTauntChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearTaunt()
    {
        tauntedUnit = null;
        turnsRemaining = 0;
        OnTauntChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool HasActiveTaunt() => tauntedUnit != null && turnsRemaining > 0;

    public Unit GetTauntedUnit() => tauntedUnit;

    public int GetTurnsRemaining() => turnsRemaining;

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        // 플레이어 턴이 시작될 때마다 카운트다운 (라운드 기준)
        if (!HasActiveTaunt()) return;
        if (!TurnSystem.Instance.IsPlayerTurn()) return;

        // 도발 유닛이 죽었으면 즉시 해제
        if (tauntedUnit == null)
        {
            ClearTaunt();
            return;
        }

        turnsRemaining--;
        if (turnsRemaining <= 0)
            ClearTaunt();
        else
            OnTauntChanged?.Invoke(this, EventArgs.Empty);
    }
}
