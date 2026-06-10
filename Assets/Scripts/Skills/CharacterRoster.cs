using UnityEngine;

/// <summary>
/// 선택 화면에 표시할 전체 캐릭터 풀.
/// Project 우클릭 → Create > Characters > CharacterRoster 로 생성.
/// </summary>
[CreateAssetMenu(fileName = "CharacterRoster", menuName = "Characters/CharacterRoster")]
public class CharacterRoster : ScriptableObject
{
    [Tooltip("선택 화면에 표시할 캐릭터 목록")]
    public CharacterData[] characters;
}
