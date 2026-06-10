using UnityEngine;

/// <summary>
/// 캐릭터 한 명의 기본 데이터를 정의하는 ScriptableObject.
/// Project 우클릭 → Create > Characters > CharacterData 로 생성.
/// </summary>
[CreateAssetMenu(fileName = "CharacterData", menuName = "Characters/CharacterData")]
public class CharacterData : ScriptableObject
{
    [Header("기본 정보")]
    public string className;
    [TextArea(1, 3)]
    public string description;
    public Sprite portrait;

    [Header("프리팹")]
    [Tooltip("PartyManager가 인스턴시에이트할 유닛 프리팹")]
    public GameObject unitPrefab;
}
