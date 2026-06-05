using UnityEngine;

/// <summary>
/// 유닛 클래스(직업)별 스킬 설정.
/// 유닛 프리팹의 Unit 컴포넌트에 할당한다.
/// Project 우클릭 → Create > Skills > UnitSkillConfig 으로 생성.
/// </summary>
[CreateAssetMenu(fileName = "UnitSkillConfig", menuName = "Skills/UnitSkillConfig")]
public class UnitSkillConfig : ScriptableObject
{
    [Tooltip("유닛 클래스를 구분하는 고유 ID. 예: Mage, Warrior, Healer")]
    public string unitClassId;

    [Tooltip("이 유닛이 보상으로 습득할 수 있는 스킬 목록. 프리팹에 비활성 컴포넌트로 반드시 존재해야 함.")]
    public SkillDefinition[] learnableSkills;
}
