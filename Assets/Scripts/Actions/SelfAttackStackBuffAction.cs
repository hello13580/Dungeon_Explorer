using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사용할 때마다 공격력 버프가 누적되는 액션.
/// </summary>
public class SelfAttackStackBuffAction : BaseAction
{
    protected override string DefaultActionName() => "공격력 집중";

    [Header("버프")]
    [SerializeField] private int attackBonusPerStack = 5;
    [SerializeField] private int duration = 3;
    [SerializeField] private int maxStacks = 5;

    [Header("애니메이션")]
    [Tooltip("애니메이션 재생 시간만큼 액션 완료를 지연시킨다.")]
    [SerializeField] private float animationDuration = 0.3f;

    [Header("이펙트")]
    [SerializeField] private GameObject buffEffectPrefab;
    [Tooltip("유닛 머리 위 기준 추가 높이 오프셋")]
    [SerializeField] private float effectHeightOffset = 0.3f;
    [SerializeField] private float effectLifetime = 2f;

    private int currentStacks = 0;

    /// <summary>버프 적용 시점에 발생. UnitAnimator 등에서 구독해 트리거를 재생한다.</summary>
    public event EventHandler OnSelfBuffStackStarted;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }

    public override string GetDescription() =>
        $"공격력을 {attackBonusPerStack} 증가시킨다. 최대 {maxStacks}번 중첩 가능. 각 중첩은 {duration}턴 지속.";

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        ActionStart(onActionComplete);
        StartCoroutine(BuffRoutine());
    }

    private IEnumerator BuffRoutine()
    {
        currentStacks++;
        unit.GetComponent<AttackBuffSystem>()?.ApplyBuff(attackBonusPerStack, duration);
        OnSelfBuffStackStarted?.Invoke(this, EventArgs.Empty);
        SpawnBuffEffect();
        yield return new WaitForSeconds(animationDuration);
        ActionComplete();
    }

    private void SpawnBuffEffect()
    {
        if (buffEffectPrefab == null) return;

        float headY = unit.GetCollider().bounds.max.y + effectHeightOffset;
        Vector3 spawnPos = new Vector3(unit.GetWorldPosition().x, headY, unit.GetWorldPosition().z);

        GameObject effect = Instantiate(buffEffectPrefab, spawnPos, Quaternion.identity);
        Destroy(effect, effectLifetime);
    }

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        // 최대 스택에 도달하면 사용 불가
        if (currentStacks >= maxStacks) return new List<GridPosition>();
        return new List<GridPosition> { unit.GetGridPosition() };
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        if (currentStacks >= maxStacks) return null;
        // 최대 스택 미만이면 최우선 사용
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 9999 };
    }
}
