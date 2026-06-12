/// <summary>
/// 맵 노드의 종류. 노드 타입에 따라 아이콘·색상 등 비주얼을 다르게 표시한다.
/// </summary>
public enum MapNodeType
{
    Combat,     // 일반 전투
    Elite,      // 강화 전투 (엘리트)
    Boss,       // 보스 전투
    Rest,       // 휴식 (체력 회복 등)
    Shop,       // 상점
}
