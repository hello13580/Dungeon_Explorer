public enum StackingMode
{
    AddValue,        // 중복 적용 시 수치 합산 (예: 피해 증폭 25% + 25% = 50%)
    ExtendDuration,  // 중복 적용 시 지속 턴 추가 (예: 둔화 2턴 + 2턴 = 4턴)
    RefreshDuration, // 중복 적용 시 지속 턴을 더 긴 쪽으로 갱신
}

/// <summary>
/// 유닛에게 적용되는 단일 상태이상 데이터.
/// </summary>
public class StatusEffect
{
    public StatusEffectType type;
    public float value;          // 효과 수치 (0.25 = 25%)
    public int turnsRemaining;   // 남은 지속 턴
    public string displayName;   // UI 표시용 이름
    public StackingMode stackingMode;

    public StatusEffect(StatusEffectType type, float value, int turns,
                        string displayName = "", StackingMode stackingMode = StackingMode.AddValue)
    {
        this.type = type;
        this.value = value;
        this.turnsRemaining = turns;
        this.displayName = string.IsNullOrEmpty(displayName) ? type.ToString() : displayName;
        this.stackingMode = stackingMode;
    }
}
