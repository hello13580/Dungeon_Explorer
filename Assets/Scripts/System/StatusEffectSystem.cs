using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛에 적용된 상태이상들을 관리하는 컴포넌트.
/// - 효과 추가/제거
/// - 매 턴(해당 유닛의 턴 시작) 지속 시간 차감
/// - 화상(Burn): 해당 유닛의 턴 종료 시 현재 화상 수치만큼 피해 → 수치 1 감소 → 0이 되면 해제
/// - 같은 종류의 효과는 StackingMode에 따라 중첩 처리
/// </summary>
public class StatusEffectSystem : MonoBehaviour
{
    private readonly List<StatusEffect> activeEffects = new List<StatusEffect>();

    public event EventHandler OnEffectsChanged;

    // 이 유닛의 턴이 시작됐는지 기억하는 플래그.
    // TurnChanged 이벤트는 "다음 유닛의 턴 시작"을 알리므로,
    // 플래그가 켜진 상태에서 이벤트가 오면 = 이 유닛의 턴이 방금 끝난 것.
    private bool isTurnActive = false;

    private void Start()
    {
        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
        StageManager.OnStageLoadingStarted += StageManager_OnStageLoadingStarted;
    }

    private void OnDestroy()
    {
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
        StageManager.OnStageLoadingStarted -= StageManager_OnStageLoadingStarted;
    }

    // 스테이지 전환 시 모든 상태이상을 즉시 해제한다
    private void StageManager_OnStageLoadingStarted(object sender, EventArgs e)
    {
        ClearAllEffects();
        isTurnActive = false;
    }

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        Unit unit = GetComponent<Unit>();
        if (unit == null) return;

        // ── 턴 종료 처리 (이 유닛의 턴이 끝나고 다음 유닛으로 넘어왔을 때) ──
        // isTurnActive가 true인데 현재 턴이 이 유닛이 아니면 = 방금 이 유닛의 턴이 끝난 것
        if (isTurnActive && TurnSystem.Instance.GetTurnUnit() != unit)
        {
            isTurnActive = false;
            ApplyBurnTick(unit);
        }

        if (TurnSystem.Instance.GetTurnUnit() != unit) return;

        // ── 턴 시작 처리 ──
        isTurnActive = true;

        // 화상은 수치 기반으로 자체 해제되므로 turnsRemaining 카운트다운에서 제외
        bool changed = false;
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            if (activeEffects[i].type == StatusEffectType.Burn) continue;

            activeEffects[i].turnsRemaining--;
            if (activeEffects[i].turnsRemaining <= 0)
            {
                activeEffects.RemoveAt(i);
                changed = true;
            }
        }
        if (changed) OnEffectsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 이 유닛의 턴 종료 시 화상 틱 처리.
    /// 현재 화상 수치만큼 피해를 주고, 수치를 1 감소시킨다.
    /// 수치가 0 이하가 되면 화상 상태이상을 제거한다.
    /// </summary>
    private void ApplyBurnTick(Unit unit)
    {
        StatusEffect burn = activeEffects.Find(e => e.type == StatusEffectType.Burn);
        if (burn == null) return;

        // 현재 화상 수치만큼 피해
        int damage = Mathf.RoundToInt(burn.value);
        if (damage > 0)
            unit.Damage(damage);

        // 화상 수치 1 감소
        burn.value -= 1f;

        if (burn.value <= 0f)
        {
            activeEffects.Remove(burn);
            OnEffectsChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            OnEffectsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // ─── 효과 추가/제거 ────────────────────────────────────────────────

    /// <summary>효과 추가. 같은 종류가 이미 있으면 StackingMode에 따라 중첩 처리.</summary>
    public void AddEffect(StatusEffect effect)
    {
        StatusEffect existing = activeEffects.Find(e => e.type == effect.type);
        if (existing != null)
        {
            switch (effect.stackingMode)
            {
                case StackingMode.AddValue:
                    existing.value += effect.value;
                    existing.turnsRemaining = Mathf.Max(existing.turnsRemaining, effect.turnsRemaining);
                    break;
                case StackingMode.ExtendDuration:
                    existing.turnsRemaining += effect.turnsRemaining;
                    break;
                case StackingMode.RefreshDuration:
                    existing.turnsRemaining = Mathf.Max(existing.turnsRemaining, effect.turnsRemaining);
                    break;
            }
        }
        else
        {
            activeEffects.Add(effect);
        }
        OnEffectsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveEffect(StatusEffectType type)
    {
        int removed = activeEffects.RemoveAll(e => e.type == type);
        if (removed > 0) OnEffectsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearAllEffects()
    {
        if (activeEffects.Count == 0) return;
        activeEffects.Clear();
        OnEffectsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ─── 수치 조회 ─────────────────────────────────────────────────────

    public bool HasEffect(StatusEffectType type)
        => activeEffects.Exists(e => e.type == type);

    public float GetTotalValue(StatusEffectType type)
    {
        float total = 0f;
        foreach (StatusEffect e in activeEffects)
            if (e.type == type) total += e.value;
        return total;
    }

    /// <summary>받는 피해 배수. 기본 1.0, DamageAmplify가 있으면 1.0 + 합산값.</summary>
    public float GetIncomingDamageMultiplier()
        => 1f + GetTotalValue(StatusEffectType.DamageAmplify);

    /// <summary>주는 피해 배수. 기본 1.0, DamageReduce(약화)가 있으면 1.0 - 합산값 (최소 0).</summary>
    public float GetOutgoingDamageMultiplier()
        => Mathf.Max(0f, 1f - GetTotalValue(StatusEffectType.DamageReduce));

    /// <summary>이동 거리 배수. 기본 1.0, MovementReduce가 있으면 1.0 - 합산값 (최소 0).</summary>
    public float GetMovementMultiplier()
        => Mathf.Max(0f, 1f - GetTotalValue(StatusEffectType.MovementReduce));

    /// <summary>속박 상태 여부. true면 이동 불가.</summary>
    public bool IsRooted() => HasEffect(StatusEffectType.Root);

    /// <summary>현재 화상 수치. 0이면 화상 없음.</summary>
    public int GetBurnStacks() => Mathf.RoundToInt(GetTotalValue(StatusEffectType.Burn));

    public List<StatusEffect> GetActiveEffects() => activeEffects;
}
