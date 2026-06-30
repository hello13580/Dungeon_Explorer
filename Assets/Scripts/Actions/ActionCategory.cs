/// <summary>
/// 액션을 두 가지로 분류한다.
/// Attack: 대미지를 입히는 액션. Tactical: 그 외 모든 액션(이동, 버프, 디버프, 힐, 유틸리티 등).
/// </summary>
public enum ActionCategory
{
    Attack,
    Tactical,
}
