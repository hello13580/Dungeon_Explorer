using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 다음 화살에 특수 효과를 장착하는 액션.
/// 사용하면 ActionSelectionUI를 통해 독/취약/약화 중 하나를 선택한다.
/// 선택 후 BowAction이 적중할 때 한 번만 효과를 적용하고 소진된다.
/// </summary>
public class ArrowEffectAction : BaseAction
{
    public enum ArrowEffectType { None, Poison, Vulnerable, Blind }

    [Header("독 바르기")]
    [SerializeField] private int poisonStacks = 3;         // 중독 초기 수치 (매 턴 1씩 감소)

    [Header("급소 사격 (취약)")]
    [SerializeField] private float vulnerableValue = 0.3f; // 받는 피해 증가 비율
    [SerializeField] private int vulnerableDuration = 2;

    [Header("실명 사격 (약화)")]
    [SerializeField] private float blindValue = 0.3f;      // 주는 피해 감소 비율
    [SerializeField] private int blindDuration = 2;

    // 현재 장착된 효과 — BowAction이 읽고 소진한다
    private ArrowEffectType pendingEffect = ArrowEffectType.None;
    private bool selectionDone = false;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }

    public override string GetActionName() => "Arrow Effect";

    public override string GetDescription() =>
        "다음 기본 사격에 특수한 효과를 부여하여 강화한다";

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        ActionStart(onActionComplete);

        var options = new List<ActionSelectionUI.OptionData>
        {
            new ActionSelectionUI.OptionData(
                "독 바르기",
                $"적중한 적에게 중독을 {poisonStacks} 부여한다."
            ),
            new ActionSelectionUI.OptionData(
                "급소 사격",
                $"적중한 적에게 취약을 부여한다.\n받는 피해 {Mathf.RoundToInt(vulnerableValue * 100)}% 증가. ({vulnerableDuration}턴)"
            ),
            new ActionSelectionUI.OptionData(
                "실명 사격",
                $"적중한 적에게 약화를 부여한다.\n주는 피해 {Mathf.RoundToInt(blindValue * 100)}% 감소. ({blindDuration}턴)"
            ),
        };

        selectionDone = false;
        ActionSelectionUI.RequestSelection("화살 효과 선택", options, OnOptionSelected);
        StartCoroutine(WaitForSelection(onActionComplete));
    }

    private void OnOptionSelected(int index)
    {
        pendingEffect = index switch
        {
            0 => ArrowEffectType.Poison,
            1 => ArrowEffectType.Vulnerable,
            2 => ArrowEffectType.Blind,
            _ => ArrowEffectType.None,
        };
        selectionDone = true;
    }

    private IEnumerator WaitForSelection(Action onActionComplete)
    {
        yield return new WaitUntil(() => selectionDone);
        ActionComplete();
    }

    /// <summary>BowAction 적중 시 호출. 장착된 효과를 대상에게 적용하고 소진한다.</summary>
    public void ApplyEffectToTarget(Unit target)
    {
        if (pendingEffect == ArrowEffectType.None) return;

        StatusEffectSystem ses = target.GetComponent<StatusEffectSystem>();
        if (ses == null) { pendingEffect = ArrowEffectType.None; return; }

        switch (pendingEffect)
        {
            case ArrowEffectType.Poison:
                ses.AddEffect(new StatusEffect(
                    StatusEffectType.Poison, poisonStacks, poisonStacks,
                    "중독", StackingMode.AddValue));
                break;

            case ArrowEffectType.Vulnerable:
                ses.AddEffect(new StatusEffect(
                    StatusEffectType.DamageAmplify, vulnerableValue, vulnerableDuration,
                    "취약", StackingMode.RefreshDuration));
                break;

            case ArrowEffectType.Blind:
                ses.AddEffect(new StatusEffect(
                    StatusEffectType.DamageReduce, blindValue, blindDuration,
                    "약화", StackingMode.RefreshDuration));
                break;
        }

        pendingEffect = ArrowEffectType.None; // 소진
    }

    public bool HasPendingEffect() => pendingEffect != ArrowEffectType.None;

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        return new List<GridPosition> { unit.GetGridPosition() };
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 60 };
    }
}
