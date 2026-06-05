using UnityEngine;

/// <summary>
/// 하나의 습득 가능한 스킬을 정의하는 ScriptableObject.
/// Project 우클릭 → Create > Skills > SkillDefinition 으로 생성.
/// </summary>
[CreateAssetMenu(fileName = "SkillDefinition", menuName = "Skills/SkillDefinition")]
public class SkillDefinition : ScriptableObject
{
    [Header("표시 정보")]
    public string skillName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;

    [Header("코드 연결")]
    [Tooltip("습득 시 활성화할 BaseAction 서브클래스의 C# 클래스명. 예: IceOrbAction")]
    public string actionTypeName;
}
