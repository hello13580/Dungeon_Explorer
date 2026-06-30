using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택된 유닛이 현재 받고 있는 버프·디버프·보호막·도발 등 모든 효과를
/// 텍스트로 나열해서 보여주는 패널. 버튼으로 열고 닫을 수 있다.
/// UnitPanelUI와 같은 방식으로 UnitActionSystem.OnSelectedUnitChanged를 구독한다.
/// </summary>
public class UnitEffectsListUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI effectsText;
    [SerializeField] private Button toggleButton;

    private Unit unit;
    private StatusEffectSystem statusEffectSystem;
    private AttackBuffSystem attackBuffSystem;
    private BarrierSystem barrierSystem;

    // 버튼으로 토글한 열림 상태. 유닛이 바뀌어도 이 상태는 유지된다.
    private bool isOpen = false;

    private void Start()
    {
        if (UnitActionSystem.Instance != null)
            UnitActionSystem.Instance.OnSelectedUnitChanged += OnSelectedUnitChanged;

        if (TauntManager.Instance != null)
            TauntManager.Instance.OnTauntChanged += OnAnyRelevantChanged;

        // 은신은 별도 변경 이벤트가 없으므로, 액션이 끝날 때마다(은신 시전·공격으로 해제 등) 갱신한다.
        BaseAction.OnAnyActionEnded += OnAnyRelevantChanged;

        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleOpen);

        ApplyVisibility();
    }

    private void OnDestroy()
    {
        if (UnitActionSystem.Instance != null)
            UnitActionSystem.Instance.OnSelectedUnitChanged -= OnSelectedUnitChanged;

        if (TauntManager.Instance != null)
            TauntManager.Instance.OnTauntChanged -= OnAnyRelevantChanged;

        BaseAction.OnAnyActionEnded -= OnAnyRelevantChanged;

        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(ToggleOpen);

        UnsubscribeUnitEvents();
    }

    /// <summary>토글 버튼의 OnClick에 연결된다. 인스펙터에서 직접 연결해도 된다.</summary>
    public void ToggleOpen()
    {
        isOpen = !isOpen;
        ApplyVisibility();
    }

    /// <summary>토글 상태와 유닛 선택 여부를 함께 반영해 패널을 켜고 끈다.</summary>
    private void ApplyVisibility()
    {
        if (panel == null) return;
        panel.SetActive(isOpen && unit != null);
    }

    private void UnsubscribeUnitEvents()
    {
        if (statusEffectSystem != null) statusEffectSystem.OnEffectsChanged -= OnAnyRelevantChanged;
        if (attackBuffSystem != null) attackBuffSystem.OnBuffChanged -= OnAnyRelevantChanged;
        if (barrierSystem != null) barrierSystem.OnBarrierChanged -= OnAnyRelevantChanged;
    }

    private void OnSelectedUnitChanged(object sender, Unit selected)
    {
        UnsubscribeUnitEvents();

        unit = selected;
        if (unit == null)
        {
            ApplyVisibility(); // 유닛 없음 → 토글 상태와 무관하게 숨김
            return;
        }

        statusEffectSystem = unit.GetComponent<StatusEffectSystem>();
        attackBuffSystem   = unit.GetComponent<AttackBuffSystem>();
        barrierSystem      = unit.GetComponent<BarrierSystem>();

        if (statusEffectSystem != null) statusEffectSystem.OnEffectsChanged += OnAnyRelevantChanged;
        if (attackBuffSystem != null) attackBuffSystem.OnBuffChanged += OnAnyRelevantChanged;
        if (barrierSystem != null) barrierSystem.OnBarrierChanged += OnAnyRelevantChanged;

        ApplyVisibility();
        Refresh();
    }

    private void OnAnyRelevantChanged(object sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        if (unit == null || effectsText == null) return;

        StringBuilder sb = new StringBuilder();

        // 은신
        if (unit.IsStealthed())
            sb.AppendLine("은신 중");

        // 보호막
        if (barrierSystem != null && barrierSystem.HasBarrier())
            sb.AppendLine($"보호막 {barrierSystem.GetTotalBarrierAmount()}");

        // 공격력 버프 (여러 출처를 합산한 총량)
        if (attackBuffSystem != null)
        {
            int bonus = attackBuffSystem.GetTotalBonus();
            if (bonus != 0)
                sb.AppendLine($"공격력 강화 +{bonus}");
        }

        // 도발 — 전역으로 한 유닛만 도발 상태일 수 있음
        if (TauntManager.Instance != null && TauntManager.Instance.GetTauntedUnit() == unit)
            sb.AppendLine($"도발 중 ({TauntManager.Instance.GetTurnsRemaining()}턴)");

        // 상태이상(버프/디버프)
        if (statusEffectSystem != null)
        {
            foreach (StatusEffect effect in statusEffectSystem.GetActiveEffects())
                sb.AppendLine(FormatStatusEffect(effect));
        }

        effectsText.text = sb.Length > 0 ? sb.ToString().TrimEnd() : "효과 없음";
    }

    /// <summary>상태이상 타입별로 보여줄 텍스트 형식을 결정한다.</summary>
    private string FormatStatusEffect(StatusEffect effect)
    {
        switch (effect.type)
        {
            case StatusEffectType.DamageAmplify:
                return $"취약 +{Mathf.RoundToInt(effect.value * 100)}% ({effect.turnsRemaining}턴)";
            case StatusEffectType.DamageReduce:
                return $"약화 -{Mathf.RoundToInt(effect.value * 100)}% ({effect.turnsRemaining}턴)";
            case StatusEffectType.MovementReduce:
                return $"둔화 -{Mathf.RoundToInt(effect.value * 100)}% ({effect.turnsRemaining}턴)";
            case StatusEffectType.Root:
                return $"속박 ({effect.turnsRemaining}턴)";
            case StatusEffectType.Burn:
                return $"화상 {Mathf.RoundToInt(effect.value)}";
            default:
                return $"{effect.displayName} ({effect.turnsRemaining}턴)";
        }
    }
}
