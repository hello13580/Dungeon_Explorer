using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사용하면 스텔스 상태가 된다.
/// 공격(Attack 카테고리) 액션을 사용하거나 지속 턴이 끝나면 스텔스가 해제된다.
/// </summary>
public class StealthAction : BaseAction
{
    protected override string DefaultActionName() => "은신";

    [SerializeField] private int duration = 3; // 지속 턴 수

    [Header("이펙트")]
    [SerializeField] private GameObject stealthEffectPrefab;
    [Tooltip("이펙트를 몸 중심 기준으로 추가 높이 오프셋")]
    [SerializeField] private float effectHeightOffset = 0f;

    private int turnsRemaining;
    private bool isStealthActive = false;
    private GameObject activeStealthEffect;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }

    private void Start()
    {
        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
        BaseAction.OnAnyActionStarted += BaseAction_OnAnyActionStarted;
        StageManager.OnStageLoadingStarted += StageManager_OnStageLoadingStarted;
    }

    private void OnDestroy()
    {
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
        BaseAction.OnAnyActionStarted -= BaseAction_OnAnyActionStarted;
        StageManager.OnStageLoadingStarted -= StageManager_OnStageLoadingStarted;
        RemoveStealthEffect();
    }

    // 스테이지 전환 시 지속 턴이 안 끝났어도 스텔스를 강제 해제한다 (StatusEffectSystem과 동일한 안전망)
    private void StageManager_OnStageLoadingStarted(object sender, EventArgs e)
    {
        if (isStealthActive) BreakStealth();
    }

    public override string GetDescription() =>
        $"스텔스 상태가 된다. 공격 스킬을 사용하거나 {duration}턴이 지나면 해제된다. 이미 스텔스 중일 때 다시 사용하면 지속 턴이 갱신된다.";

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        ActionStart(onActionComplete);
        StartCoroutine(StealthRoutine());
    }

    private IEnumerator StealthRoutine()
    {
        isStealthActive = true;
        turnsRemaining = duration;
        unit.SetStealth(true);
        SpawnStealthEffect();

        yield return new WaitForSeconds(0.3f);
        ActionComplete();
    }

    /// <summary>몸 중심에 이펙트를 붙인다. 이미 떠 있으면 새로 만들지 않는다(재사용 시 갱신만).</summary>
    private void SpawnStealthEffect()
    {
        if (stealthEffectPrefab == null) return;
        if (activeStealthEffect != null) return;

        Vector3 spawnPos = unit.GetCollider().bounds.center + Vector3.up * effectHeightOffset;
        activeStealthEffect = Instantiate(stealthEffectPrefab, spawnPos, Quaternion.identity, unit.transform);
    }

    private void RemoveStealthEffect()
    {
        if (activeStealthEffect == null) return;
        Destroy(activeStealthEffect);
        activeStealthEffect = null;
    }

    /// <summary>이 유닛이 공격(Attack) 액션을 시작하면 스텔스가 즉시 해제된다.</summary>
    private void BaseAction_OnAnyActionStarted(object sender, EventArgs e)
    {
        if (!isStealthActive) return;
        if (sender is not BaseAction action) return;
        if (action.GetUnit() != unit) return;
        if (action == this) return; // 은신 사용 자체로는 해제되지 않음 (Tactical이라 어차피 안 걸림, 안전망)
        if (action.GetActionCategory() != ActionCategory.Attack) return;

        BreakStealth();
    }

    /// <summary>이 유닛의 턴이 시작될 때마다 지속 턴을 1씩 감소시키고, 0이 되면 해제한다.</summary>
    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        if (!isStealthActive) return;
        if (TurnSystem.Instance.GetTurnUnit() != unit) return;

        turnsRemaining--;
        if (turnsRemaining <= 0)
            BreakStealth();
    }

    private void BreakStealth()
    {
        isStealthActive = false;
        unit.SetStealth(false);
        RemoveStealthEffect();
    }

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        // 스텔스 중에도 재사용 가능 — 지속 턴 갱신 용도
        return new List<GridPosition> { unit.GetGridPosition() };
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        // 이미 스텔스 중이고 지속 턴이 충분히 남았으면 굳이 다시 쓸 필요 없음
        if (isStealthActive && turnsRemaining > 1) return null;
        // 체력이 낮을수록 생존 목적으로 우선 사용
        int missingHealthPercent = 100 - Mathf.RoundToInt(unit.GetHealthNormalized() * 100f);
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 50 + missingHealthPercent };
    }
}
