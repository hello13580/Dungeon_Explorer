public enum StatusEffectType
{
    DamageAmplify,      // 받는 피해 증가 (value = 0.25 → 25% 증가)
    DamageReduce,       // 주는 피해 감소 (value = 0.30 → 30% 감소) — 약화/실명
    MovementReduce,     // 이동 거리 감소 (value = 0.30 → 30% 감소)
    Root,               // 속박 — 이동 불가 (value 미사용)
    Burn,               // 화상 — 턴 종료 시 value만큼 피해, 이후 value 1 감소. value가 0이 되면 해제
    Regen,              // 재생 — 턴 종료 시 value만큼 회복, 이후 value 1 감소. value가 0이 되면 해제
}
