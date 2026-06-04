using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유닛에 적용된 상태이상들을 관리하는 컴포넌트.
/// - 효과 추가/제거
/// - 매 턴(해당 유닛의 턴) 지속 시간 차감
/// - 같은 종류의 효과는 수치 합산(중첩)
/// </summary>
public class StatusEffectSystem : MonoBehaviour
{
    private readonly List<StatusEffect> activeEffects = new List<StatusEffect>();

    public event EventHandler OnEffectsChanged;

    private void Start()
    {
        TurnSystem.Instance.OnTurnChanged += TurnSystem_OnTurnChanged;
    }

    private void OnDestroy()
    {
        if (TurnSystem.Instance != null)
            TurnSystem.Instance.OnTurnChanged -= TurnSystem_OnTurnChanged;
    }

    private void TurnSystem_OnTurnChanged(object sender, EventArgs e)
    {
        // 이 유닛의 턴이 시작될 때만 카운트다운
        Unit unit = GetComponent<Unit>();
        if (unit == null) return;
        if (TurnSystem.Instance.GetTurnUnit() != unit) return;

        bool changed = false;
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            activeEffects[i].turnsRemaining--;
            if (activeEffects[i].turnsRemaining <= 0)
            {
                activeEffects.RemoveAt(i);
                changed = true;
            }
        }
        if (changed) OnEffectsChanged?.Invoke(this, EventArgs.Empty);
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

    /// <summary>이동 거리 배수. 기본 1.0, MovementReduce가 있으면 1.0 - 합산값 (최소 0).</summary>
    public float GetMovementMultiplier()
        => Mathf.Max(0f, 1f - GetTotalValue(StatusEffectType.MovementReduce));

    public List<StatusEffect> GetActiveEffects() => activeEffects;
}
