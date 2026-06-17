/// <summary>
/// 직업 포인트(JP)의 직업 종류.
/// JobPointSystem.classId와 BaseAction.requiredJobClass에서 공통으로 사용한다.
/// None은 JP를 소모하지 않는 액션에 사용한다.
/// </summary>
public enum JobClass
{
    None,       // JP 소모 없음 (일반 액션)
    Common,     // 공용 — 어떤 직업이든 사용 가능한 JP
    Warrior,    // 전사
    Mage,       // 마법사
    Cleric,     // 클레릭
    Archer,     // 궁수
}
