using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 자기 자신에게 공격력 버프를 거는 액션.
/// </summary>
public class SelfAttackBuffAction : BaseAction
{
    protected override string DefaultActionName() => "공격력 강화";

    [Header("버프")]
    [SerializeField] private int attackBonus = 10;
    [SerializeField] private int duration = 3;

    [Header("애니메이션")]
    [Tooltip("애니메이션 재생 시간만큼 액션 완료를 지연시킨다.")]
    [SerializeField] private float animationDuration = 0.3f;

    [Header("이펙트")]
    [SerializeField] private GameObject buffEffectPrefab;
    [Tooltip("유닛 머리 위 기준 추가 높이 오프셋")]
    [SerializeField] private float effectHeightOffset = 0.3f;
    [SerializeField] private float effectLifetime = 2f;

    /// <summary>버프 적용 시점에 발생. UnitAnimator 등에서 구독해 트리거를 재생한다.</summary>
    public event EventHandler OnSelfBuffStarted;

    protected override void Awake()
    {
        base.Awake();
        actionCost = 1;
    }

    public override string GetDescription() => $"공격력을 {attackBonus} 만큼 {duration}턴 동안 증가시킨다. 이 효과는 중첩되지 않으며 재사용 시 지속 턴을 갱신한다";

    public override void TakeAction(GridPosition gridPosition, Action onActionComplete)
    {
        ActionStart(onActionComplete);
        StartCoroutine(BuffRoutine());
    }

    private IEnumerator BuffRoutine()
    {
        unit.GetComponent<AttackBuffSystem>()?.ApplyOrRefreshBuff("SelfAttackBuff", attackBonus, duration);
        OnSelfBuffStarted?.Invoke(this, EventArgs.Empty);
        SpawnBuffEffect();
        yield return new WaitForSeconds(animationDuration);
        ActionComplete();
    }

    private void SpawnBuffEffect()
    {
        if (buffEffectPrefab == null) return;

        // 유닛 콜라이더 최상단(머리 위)을 기준으로 추가 오프셋만큼 띄워서 스폰
        float headY = unit.GetCollider().bounds.max.y + effectHeightOffset;
        Vector3 spawnPos = new Vector3(unit.GetWorldPosition().x, headY, unit.GetWorldPosition().z);

        GameObject effect = Instantiate(buffEffectPrefab, spawnPos, Quaternion.identity);
        Destroy(effect, effectLifetime);
    }

    public override List<GridPosition> GetValidActionGridPositionList()
    {
        return new List<GridPosition> { unit.GetGridPosition() };
    }

    public override EnemyAIAction GetEnemyAIAction(GridPosition gridPosition)
    {
        // 이미 버프가 걸려 있으면 사용하지 않음
        AttackBuffSystem abs = unit.GetComponent<AttackBuffSystem>();
        if (abs != null && abs.GetTotalBonus() > 0) return null;

        // 공격 액션(밀리 200, 보우 1000+)보다 낮은 우선순위
        return new EnemyAIAction { gridPosition = gridPosition, actionValue = 50 };
    }
}
